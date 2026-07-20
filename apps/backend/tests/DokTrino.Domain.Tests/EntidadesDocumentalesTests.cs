using DokTrino.Domain.Entities;

namespace DokTrino.Domain.Tests;

/// <summary>
/// Invariantes del modelo documental: los valores por defecto con los que nace
/// cada entidad son parte del contrato (el resto del sistema depende de ellos).
/// </summary>
public class EntidadesDocumentalesTests
{
    [Fact]
    public void ArchivoDigital_nace_pendiente_sin_identificar_y_en_gestion()
    {
        var a = new ArchivoDigital();

        // Estos tres defaults deciden en que bandeja cae el documento recien
        // cargado y en que fase del ciclo lo cuenta el dashboard.
        Assert.Equal("PENDIENTE", a.EstadoAprobacion);
        Assert.False(a.FlagIdentificado);
        Assert.Equal("GESTION", a.FaseArchivistica);
        Assert.True(a.Activo);
    }

    [Fact]
    public void ArchivoDigital_nace_sin_expediente_ni_dependencia()
    {
        var a = new ArchivoDigital();

        Assert.Null(a.ExpedienteId);
        Assert.Null(a.DependenciaId);
    }

    [Fact]
    public void Trd_nace_en_desarrollo()
    {
        var t = new TablaRetencionDocumental();

        Assert.Equal("DESARROLLO", t.Estado);
    }

    [Fact]
    public void Catalogo_nace_como_maestro_y_sin_dependencia_que_lo_sugiera()
    {
        var serie = new Serie();
        var subserie = new Subserie();
        var tipologia = new TipologiaDocumental();

        // Si el default fuera SUGERIDA, el catalogo oficial quedaria invisible
        // para las dependencias que no lo crearon.
        Assert.Equal("MAESTRA", serie.Estado);
        Assert.Equal("MAESTRA", subserie.Estado);
        Assert.Equal("MAESTRA", tipologia.Estado);

        Assert.Null(serie.SugeridaPorDependenciaId);
        Assert.Null(subserie.SugeridaPorDependenciaId);
        Assert.Null(tipologia.SugeridaPorDependenciaId);
    }

    [Fact]
    public void Expediente_nace_abierto()
    {
        var e = new Expediente();

        Assert.Equal("ABIERTO", e.Estado);
    }

    [Fact]
    public void ElementoTopografico_nace_disponible_y_vacio()
    {
        var e = new ElementoTopografico();

        Assert.Equal("DISPONIBLE", e.Estado);
        Assert.Equal(0, e.Ocupacion);
    }

    [Fact]
    public void Cuestionario_tiene_corte_por_defecto_de_sesenta()
    {
        var c = new CuestionarioCapacitacion();

        Assert.Equal("FORMACION_TRD", c.Modulo);
        Assert.Equal(60, c.PuntajeMinimo);
        Assert.True(c.Activo);
    }

    [Fact]
    public void FormacionDependencia_muestra_el_hint_hasta_que_se_cierre()
    {
        var f = new FormacionDependencia();

        Assert.True(f.MostrarHint);
        Assert.False(f.Superado);
    }

    [Fact]
    public void ProcesoDefinicion_nace_sin_publicar_y_sin_diagrama()
    {
        var p = new ProcesoDefinicion();

        // Sin esto se podrian iniciar instancias de un proceso sin grafo.
        Assert.False(p.Publicado);
        Assert.Null(p.BpmnXml);
    }

    [Fact]
    public void ProcesoNodo_por_defecto_es_tarea()
    {
        var n = new ProcesoNodo();

        Assert.Equal("TAREA", n.Tipo);
    }

    [Fact]
    public void Ids_son_guid_v7_distintos_y_ordenables_en_el_tiempo()
    {
        var primero = new Expediente().Id;
        Thread.Sleep(2);
        var segundo = new Expediente().Id;

        Assert.NotEqual(Guid.Empty, primero);
        Assert.NotEqual(primero, segundo);

        // Guid v7 lleva el timestamp al frente: el creado despues ordena despues.
        Assert.True(string.CompareOrdinal(primero.ToString(), segundo.ToString()) < 0);
    }
}
