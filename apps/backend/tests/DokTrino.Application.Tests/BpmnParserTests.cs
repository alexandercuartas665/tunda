using DokTrino.Application.Bpmn;

namespace DokTrino.Application.Tests;

/// <summary>
/// El parser es la unica barrera entre un diagrama dibujado a mano y el motor.
/// Si deja pasar un grafo incompleto, la instancia se queda colgada en runtime.
/// </summary>
public class BpmnParserTests
{
    private const string FlujoValido = """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="D1">
          <bpmn:process id="P1" isExecutable="true">
            <bpmn:startEvent id="Start_1" name="PQRS recibida" />
            <bpmn:userTask id="Task_1" name="Analizar peticion" />
            <bpmn:endEvent id="End_1" name="Respuesta enviada" />
            <bpmn:sequenceFlow id="F1" sourceRef="Start_1" targetRef="Task_1" />
            <bpmn:sequenceFlow id="F2" sourceRef="Task_1" targetRef="End_1" />
          </bpmn:process>
        </bpmn:definitions>
        """;

    [Fact]
    public void Flujo_completo_es_valido_y_mapea_nodos_y_transiciones()
    {
        var r = BpmnParser.Parsear(FlujoValido);

        Assert.True(r.EsValido);
        Assert.Equal(3, r.Nodos.Count);
        Assert.Equal(2, r.Transiciones.Count);

        Assert.Equal("INICIO", r.Nodos.Single(n => n.ElementoBpmnId == "Start_1").Tipo);
        Assert.Equal("TAREA", r.Nodos.Single(n => n.ElementoBpmnId == "Task_1").Tipo);
        Assert.Equal("FIN", r.Nodos.Single(n => n.ElementoBpmnId == "End_1").Tipo);
        Assert.Equal("PQRS recibida", r.Nodos.Single(n => n.ElementoBpmnId == "Start_1").Nombre);
    }

    [Fact]
    public void Xml_vacio_no_es_valido()
    {
        Assert.False(BpmnParser.Parsear("").EsValido);
        Assert.False(BpmnParser.Parsear(null).EsValido);
        Assert.False(BpmnParser.Parsear("   ").EsValido);
    }

    [Fact]
    public void Xml_malformado_reporta_error_en_vez_de_reventar()
    {
        var r = BpmnParser.Parsear("<bpmn:definitions><sin cerrar>");

        Assert.False(r.EsValido);
        Assert.Contains(r.Errores, e => e.Contains("no es valido", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Xml_sin_process_no_es_valido()
    {
        var r = BpmnParser.Parsear("""<?xml version="1.0"?><root />""");

        Assert.False(r.EsValido);
        Assert.Contains(r.Errores, e => e.Contains("process", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Solo_evento_de_inicio_falla_por_falta_de_fin_y_de_salida()
    {
        var xml = """
            <?xml version="1.0"?>
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <bpmn:process id="P1"><bpmn:startEvent id="S1" name="Inicio" /></bpmn:process>
            </bpmn:definitions>
            """;

        var r = BpmnParser.Parsear(xml);

        Assert.False(r.EsValido);
        Assert.Contains(r.Errores, e => e.Contains("evento de fin", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Errores, e => e.Contains("no tiene salida", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Nodo_inalcanzable_se_reporta()
    {
        // Task_2 no recibe ninguna transicion: nunca se ejecutaria.
        var xml = """
            <?xml version="1.0"?>
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <bpmn:process id="P1">
                <bpmn:startEvent id="S1" name="Inicio" />
                <bpmn:task id="T1" name="Uno" />
                <bpmn:task id="T2" name="Huerfana" />
                <bpmn:endEvent id="E1" name="Fin" />
                <bpmn:sequenceFlow id="F1" sourceRef="S1" targetRef="T1" />
                <bpmn:sequenceFlow id="F2" sourceRef="T1" targetRef="E1" />
                <bpmn:sequenceFlow id="F3" sourceRef="T2" targetRef="E1" />
              </bpmn:process>
            </bpmn:definitions>
            """;

        var r = BpmnParser.Parsear(xml);

        Assert.False(r.EsValido);
        Assert.Contains(r.Errores, e => e.Contains("Huerfana") && e.Contains("alcanzable"));
    }

    [Fact]
    public void Falta_evento_de_inicio_se_reporta()
    {
        var xml = """
            <?xml version="1.0"?>
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <bpmn:process id="P1">
                <bpmn:task id="T1" name="Uno" />
                <bpmn:endEvent id="E1" name="Fin" />
                <bpmn:sequenceFlow id="F1" sourceRef="T1" targetRef="E1" />
              </bpmn:process>
            </bpmn:definitions>
            """;

        var r = BpmnParser.Parsear(xml);

        Assert.False(r.EsValido);
        Assert.Contains(r.Errores, e => e.Contains("evento de inicio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compuerta_se_reconoce_y_admite_condicion_en_la_transicion()
    {
        var xml = """
            <?xml version="1.0"?>
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <bpmn:process id="P1">
                <bpmn:startEvent id="S1" name="Inicio" />
                <bpmn:exclusiveGateway id="G1" name="Procede?" />
                <bpmn:task id="T1" name="Aprobar" />
                <bpmn:endEvent id="E1" name="Fin" />
                <bpmn:sequenceFlow id="F1" sourceRef="S1" targetRef="G1" />
                <bpmn:sequenceFlow id="F2" sourceRef="G1" targetRef="T1">
                  <bpmn:conditionExpression>monto &gt; 100</bpmn:conditionExpression>
                </bpmn:sequenceFlow>
                <bpmn:sequenceFlow id="F3" sourceRef="T1" targetRef="E1" />
              </bpmn:process>
            </bpmn:definitions>
            """;

        var r = BpmnParser.Parsear(xml);

        Assert.True(r.EsValido);
        Assert.Equal("COMPUERTA", r.Nodos.Single(n => n.ElementoBpmnId == "G1").Tipo);
        Assert.Equal("monto > 100", r.Transiciones.Single(t => t.ElementoBpmnId == "F2").Condicion);
        Assert.Null(r.Transiciones.Single(t => t.ElementoBpmnId == "F1").Condicion);
    }

    [Fact]
    public void Tarea_sin_nombre_recibe_etiqueta_por_defecto()
    {
        var xml = """
            <?xml version="1.0"?>
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <bpmn:process id="P1">
                <bpmn:startEvent id="S1" />
                <bpmn:task id="T1" />
                <bpmn:endEvent id="E1" />
                <bpmn:sequenceFlow id="F1" sourceRef="S1" targetRef="T1" />
                <bpmn:sequenceFlow id="F2" sourceRef="T1" targetRef="E1" />
              </bpmn:process>
            </bpmn:definitions>
            """;

        var r = BpmnParser.Parsear(xml);

        Assert.True(r.EsValido);
        Assert.Equal("Tarea sin nombre", r.Nodos.Single(n => n.ElementoBpmnId == "T1").Nombre);
        Assert.Equal("Inicio", r.Nodos.Single(n => n.ElementoBpmnId == "S1").Nombre);
    }

    [Fact]
    public void Transicion_que_apunta_a_un_elemento_inexistente_se_reporta()
    {
        var xml = """
            <?xml version="1.0"?>
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <bpmn:process id="P1">
                <bpmn:startEvent id="S1" name="Inicio" />
                <bpmn:endEvent id="E1" name="Fin" />
                <bpmn:sequenceFlow id="F1" sourceRef="S1" targetRef="NoExiste" />
                <bpmn:sequenceFlow id="F2" sourceRef="S1" targetRef="E1" />
              </bpmn:process>
            </bpmn:definitions>
            """;

        var r = BpmnParser.Parsear(xml);

        Assert.False(r.EsValido);
        Assert.Contains(r.Errores, e => e.Contains("F1") && e.Contains("desconocido"));
    }
}
