using DokTrino.Application.Tenancy;

namespace DokTrino.Application.Tests;

/// <summary>
/// El guard es la unica defensa entre el SQL que escribe un administrador y la
/// base de datos del tenant. Cada caso que deje pasar es una via de escritura.
/// </summary>
public class BiSqlGuardTests
{
    [Theory]
    [InlineData("select codigo, nombre from series")]
    [InlineData("SELECT * FROM series WHERE tenant_id = @tenant")]
    [InlineData("  select 1  ")]
    [InlineData("with reciente as (select 1) select * from reciente")]
    public void Acepta_consultas_de_solo_lectura(string sql)
    {
        Assert.True(BiSqlGuard.EsSelectSeguro(sql));
    }

    [Theory]
    [InlineData("delete from series")]
    [InlineData("DROP TABLE series")]
    [InlineData("update series set nombre = 'x'")]
    [InlineData("insert into series values (1)")]
    [InlineData("truncate series")]
    [InlineData("alter table series add column x int")]
    [InlineData("create table x (id int)")]
    [InlineData("grant all on series to public")]
    public void Rechaza_escritura_y_ddl(string sql)
    {
        Assert.False(BiSqlGuard.EsSelectSeguro(sql));
    }

    [Fact]
    public void Rechaza_varias_sentencias_encadenadas()
    {
        // El vector clasico: una consulta legitima seguida de la destructiva.
        Assert.False(BiSqlGuard.EsSelectSeguro("select 1; drop table series"));
    }

    [Fact]
    public void Rechaza_comentarios_que_esconden_el_resto()
    {
        Assert.False(BiSqlGuard.EsSelectSeguro("select 1 -- ocultar el resto"));
        Assert.False(BiSqlGuard.EsSelectSeguro("select 1 /* bloque */"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rechaza_lo_vacio(string? sql)
    {
        Assert.False(BiSqlGuard.EsSelectSeguro(sql));
    }

    [Fact]
    public void Acepta_el_punto_y_coma_final_pero_no_uno_intermedio()
    {
        Assert.True(BiSqlGuard.EsSelectSeguro("select 1;"));
        Assert.False(BiSqlGuard.EsSelectSeguro("select 1; select 2;"));
    }

    [Fact]
    public void Rechaza_lo_que_no_empieza_por_select_o_with()
    {
        Assert.False(BiSqlGuard.EsSelectSeguro("explain select 1"));
        Assert.False(BiSqlGuard.EsSelectSeguro("copy series to '/tmp/x'"));
    }

    [Fact]
    public void No_se_deja_enganiar_por_mayusculas_ni_espacios()
    {
        Assert.False(BiSqlGuard.EsSelectSeguro("   DeLeTe   FROM series"));
        Assert.True(BiSqlGuard.EsSelectSeguro("   SeLeCt   1"));
    }
}
