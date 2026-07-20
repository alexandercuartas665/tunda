using System.Xml.Linq;

namespace DokTrino.Application.Bpmn;

public sealed record NodoParseado(string ElementoBpmnId, string Tipo, string Nombre);
public sealed record TransicionParseada(string ElementoBpmnId, string Origen, string Destino, string? Nombre, string? Condicion);

/// <summary>Grafo extraido del XML, ya validado.</summary>
public sealed record ProcesoParseado(
    IReadOnlyList<NodoParseado> Nodos,
    IReadOnlyList<TransicionParseada> Transiciones,
    IReadOnlyList<string> Errores)
{
    public bool EsValido => Errores.Count == 0;
}

/// <summary>
/// Lee un diagrama BPMN 2.0 y lo traduce a nodos y transiciones. Solo entiende
/// el subconjunto que el motor sabe ejecutar: eventos de inicio y fin, tareas y
/// compuertas exclusivas.
/// </summary>
public static class BpmnParser
{
    private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";

    public static ProcesoParseado Parsear(string? xml)
    {
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return new ProcesoParseado([], [], ["El diagrama esta vacio."]);
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            return new ProcesoParseado([], [], [$"El XML no es valido: {ex.Message}"]);
        }

        var proceso = doc.Descendants(Bpmn + "process").FirstOrDefault();
        if (proceso is null)
        {
            return new ProcesoParseado([], [], ["El diagrama no contiene ningun <process>."]);
        }

        var nodos = new List<NodoParseado>();
        foreach (var (elemento, tipo) in new (string, string)[]
                 {
                     ("startEvent", "INICIO"),
                     ("task", "TAREA"),
                     ("userTask", "TAREA"),
                     ("serviceTask", "TAREA"),
                     ("manualTask", "TAREA"),
                     ("exclusiveGateway", "COMPUERTA"),
                     ("inclusiveGateway", "COMPUERTA"),
                     ("parallelGateway", "COMPUERTA"),
                     ("endEvent", "FIN")
                 })
        {
            foreach (var e in proceso.Elements(Bpmn + elemento))
            {
                var id = (string?)e.Attribute("id");
                if (string.IsNullOrWhiteSpace(id)) { continue; }
                var nombre = (string?)e.Attribute("name");
                nodos.Add(new NodoParseado(id, tipo, string.IsNullOrWhiteSpace(nombre) ? EtiquetaPorDefecto(tipo) : nombre.Trim()));
            }
        }

        var transiciones = new List<TransicionParseada>();
        foreach (var f in proceso.Elements(Bpmn + "sequenceFlow"))
        {
            var id = (string?)f.Attribute("id");
            var origen = (string?)f.Attribute("sourceRef");
            var destino = (string?)f.Attribute("targetRef");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(origen) || string.IsNullOrWhiteSpace(destino))
            {
                continue;
            }

            var condicion = f.Element(Bpmn + "conditionExpression")?.Value;
            transiciones.Add(new TransicionParseada(
                id, origen, destino, (string?)f.Attribute("name"),
                string.IsNullOrWhiteSpace(condicion) ? null : condicion.Trim()));
        }

        // --- Validaciones minimas para que el motor pueda ejecutarlo ---
        var ids = nodos.Select(n => n.ElementoBpmnId).ToHashSet(StringComparer.Ordinal);

        if (!nodos.Any(n => n.Tipo == "INICIO"))
        {
            errores.Add("Falta un evento de inicio.");
        }
        if (!nodos.Any(n => n.Tipo == "FIN"))
        {
            errores.Add("Falta un evento de fin.");
        }

        foreach (var t in transiciones)
        {
            if (!ids.Contains(t.Origen)) { errores.Add($"La transicion {t.ElementoBpmnId} sale de un elemento desconocido."); }
            if (!ids.Contains(t.Destino)) { errores.Add($"La transicion {t.ElementoBpmnId} llega a un elemento desconocido."); }
        }

        // Un nodo que no sea de inicio y no reciba nada nunca se alcanza.
        var conEntrada = transiciones.Select(t => t.Destino).ToHashSet(StringComparer.Ordinal);
        foreach (var n in nodos.Where(n => n.Tipo != "INICIO" && !conEntrada.Contains(n.ElementoBpmnId)))
        {
            errores.Add($"\"{n.Nombre}\" no es alcanzable: ninguna transicion llega a el.");
        }

        // Un nodo que no sea final y no salga a ningun lado deja la instancia colgada.
        var conSalida = transiciones.Select(t => t.Origen).ToHashSet(StringComparer.Ordinal);
        foreach (var n in nodos.Where(n => n.Tipo != "FIN" && !conSalida.Contains(n.ElementoBpmnId)))
        {
            errores.Add($"\"{n.Nombre}\" no tiene salida: la instancia quedaria detenida ahi.");
        }

        return new ProcesoParseado(nodos, transiciones, errores);
    }

    private static string EtiquetaPorDefecto(string tipo) => tipo switch
    {
        "INICIO" => "Inicio",
        "FIN" => "Fin",
        "COMPUERTA" => "Decision",
        _ => "Tarea sin nombre"
    };
}
