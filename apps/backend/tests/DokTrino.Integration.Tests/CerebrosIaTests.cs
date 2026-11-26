using System.Text.Json;
using DokTrino.Application.Common;
using DokTrino.Application.Common.Auth;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using DokTrino.Infrastructure.Persistence;
using DokTrino.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DokTrino.Integration.Tests;

/// <summary>
/// Cerebros en la IA (ola 3): herramientas de solo lectura acotadas al tenant (y a los
/// cerebros de referencia), el Procedimiento TRD que las consulta cuando hay cerebros
/// marcados, y el chat del agente que respeta sus herramientas habilitadas. El proveedor
/// de IA es falso: no sale nada a internet.
/// </summary>
public sealed class CerebrosIaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private Guid _tenant, _otro, _admin, _asesor;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await using var ctx = Contexto(null, null);
        await ctx.Database.MigrateAsync();
        _tenant = Guid.CreateVersion7();
        _otro = Guid.CreateVersion7();
        ctx.Tenants.Add(new Tenant { Id = _tenant, Name = "Entidad" });
        ctx.Tenants.Add(new Tenant { Id = _otro, Name = "Otra" });
        var admin = new PlatformUser { Email = "admin@e.qa", DisplayName = "Admin", Status = PlatformUserStatus.Active };
        var asesor = new PlatformUser { Email = "asesor@e.qa", DisplayName = "Asesor", Status = PlatformUserStatus.Active };
        ctx.PlatformUsers.AddRange(admin, asesor);
        (_admin, _asesor) = (admin.Id, asesor.Id);
        ctx.TenantUsers.Add(new TenantUser { TenantId = _tenant, PlatformUserId = _admin, Email = "admin@e.qa", TenantRole = TenantRole.Admin });
        ctx.TenantUsers.Add(new TenantUser { TenantId = _tenant, PlatformUserId = _asesor, Email = "asesor@e.qa", TenantRole = TenantRole.Advisor });
        // Proveedor de IA de la plataforma (la clave la "descifra" el protector falso).
        ctx.AiProviderConfigs.Add(new AiProviderConfig { Provider = AiProvider.Claude, IsEnabled = true, ApiKeyEncrypted = "clave", Model = "modelo-prueba" });
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Herramientas_de_cerebros_buscan_y_leen_solo_lo_del_tenant_y_su_alcance()
    {
        Guid normativa, privado;
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var svc = Cerebros(ctx);
            normativa = (await svc.CrearAsync("Normativa", null, null)).Id;
            privado = (await svc.CrearAsync("Privado", null, null)).Id;
            await svc.GuardarNotaAsync(normativa, null, "Tabla de plazos", "Los contratos se conservan 20 anios. Ver [[Ley 594]].");
            await svc.GuardarNotaAsync(normativa, null, "Ley 594", "Ley general de archivos.");
            await svc.GuardarNotaAsync(privado, null, "Contratos internos", "contratos de la casa");
        }

        await using var ctxA = Contexto(_tenant, _admin);
        var todos = new CerebroToolset(ctxA);
        Assert.Equal([CerebroToolset.ToolListar, CerebroToolset.ToolBuscar, CerebroToolset.ToolVer], todos.GetSpecs().Select(s => s.Name).ToArray());

        var lista = await todos.ExecuteAsync(CerebroToolset.ToolListar, "{}");
        Assert.True(lista.Ok);
        Assert.Contains("normativa", lista.Json);

        // El titulo pesa mas: "Tabla de plazos" (titulo + 2 del cuerpo) le gana a "Contratos internos" (titulo + 1).
        var busqueda = await todos.ExecuteAsync(CerebroToolset.ToolBuscar, """{"texto":"plazos conservan contratos"}""");
        using (var doc = JsonDocument.Parse(busqueda.Json))
        {
            var primero = doc.RootElement.GetProperty("resultados")[0];
            Assert.Equal("Tabla de plazos", primero.GetProperty("titulo").GetString());
        }

        var nota = await todos.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"normativa","titulo":"tabla de PLAZOS"}""");
        Assert.True(nota.Ok);
        Assert.Contains("20 anios", nota.Json);
        Assert.Contains("Ley 594", nota.Json); // enlaza_a
        Assert.Contains(todos.Fuentes, f => f.Titulo == "Tabla de plazos" && f.Leida);
        Assert.Equal(2, todos.Consultas);

        // Acotado a "Normativa": el cerebro privado no existe para el agente.
        var acotado = CerebroToolset.SoloCerebros(ctxA, [normativa]);
        var b2 = await acotado.ExecuteAsync(CerebroToolset.ToolBuscar, """{"texto":"contratos"}""");
        Assert.DoesNotContain("Contratos internos", b2.Json);
        Assert.False((await acotado.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"privado","titulo":"Contratos internos"}""")).Ok);

        // Otro tenant no ve nada de esta entidad.
        await using var ctxOtro = Contexto(_otro, null);
        var ajeno = new CerebroToolset(ctxOtro);
        Assert.DoesNotContain("normativa", (await ajeno.ExecuteAsync(CerebroToolset.ToolListar, "{}")).Json);
        Assert.False((await ajeno.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"normativa","titulo":"Ley 594"}""")).Ok);
        Assert.False((await ajeno.ExecuteAsync("borrar_todo", "{}")).Ok);
    }

    [Fact]
    public async Task Herramientas_de_cerebros_entregan_solo_texto_nunca_los_PDF()
    {
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var svc = Cerebros(ctx);
            var id = (await svc.CrearAsync("Minas", null, null)).Id;
            await svc.GuardarNotaAsync(id, null, "Resolucion 01738 de 2026",
                "---\nnumero: \"01738\"\nfuente_pdf: \"99 Fuentes PDF/PDF/Res_01738_2026-09-24.pdf\"\n---\n" +
                "Subsidios FOES. | [[99 Fuentes PDF/PDF/Res_01738_2026-09-24.pdf|PDF]] | ![[anexo.png]] | [copia](docs/res.pdf) | " +
                "[[99 Fuentes PDF/Transcripciones/Res_01738_2026-09-24|Transcripcion]]");
            await svc.GuardarNotaAsync(id, null, "Res_01738_2026-09-24", "Texto oficial: ordena el giro de subsidios FOES.");
        }

        await using var ctxA = Contexto(_tenant, _admin);
        var tool = new CerebroToolset(ctxA);

        var ficha = await tool.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"minas","titulo":"Resolucion 01738 de 2026"}""");
        Assert.True(ficha.Ok);
        Assert.DoesNotContain(".pdf", ficha.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".png", ficha.Json);
        Assert.Contains("numero", ficha.Json);           // las demas propiedades siguen
        Assert.Contains("Res_01738_2026-09-24", ficha.Json); // enlace a la transcripcion (texto)

        // Pedir el PDF entrega la transcripcion de mismo nombre, nunca el archivo.
        var pdf = await tool.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"minas","titulo":"99 Fuentes PDF/PDF/Res_01738_2026-09-24.pdf"}""");
        Assert.True(pdf.Ok);
        Assert.Contains("Texto oficial", pdf.Json);
        Assert.Contains(tool.Fuentes, f => f.Titulo == "Res_01738_2026-09-24" && f.Leida);

        // Un PDF sin transcripcion: error que explica que solo hay texto.
        var sin = await tool.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"minas","titulo":"otro.pdf"}""");
        Assert.False(sin.Ok);
        Assert.Contains("no se cargan", sin.Json);

        var busqueda = await tool.ExecuteAsync(CerebroToolset.ToolBuscar, """{"texto":"FOES subsidios"}""");
        Assert.DoesNotContain(".pdf", busqueda.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Procedimiento_TRD_consulta_los_cerebros_marcados_como_referencia()
    {
        var respuestaId = await CrearRespuestaAsync();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var svc = Cerebros(ctx);
            var normativa = (await svc.CrearAsync("Normativa", null, null)).Id;
            var privado = (await svc.CrearAsync("Privado", null, null)).Id;
            await svc.GuardarNotaAsync(normativa, null, "Tabla de plazos", "Los contratos se conservan 20 anios.");
            await svc.GuardarNotaAsync(privado, null, "Contratos internos", "contratos de la casa");

            // Solo el admin marca la referencia de IA.
            await Assert.ThrowsAsync<InvalidOperationException>(() => Cerebros(Contexto(_tenant, _asesor)).MarcarUsoIaAsync(normativa, true));
            await svc.MarcarUsoIaAsync(normativa, true);
        }

        var cliente = new ClienteIaFalso();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var res = await Inferencia(ctx, cliente).GenerarProcedimientoAsync(respuestaId);
            Assert.True(res.Ok, res.Error);
            Assert.Contains("PLAZOS", res.QueEs);
        }

        // Que es + por que se elimina usaron herramientas, acotadas a "Normativa" y con la instruccion.
        Assert.True(cliente.PromptsConHerramientas.Count >= 2);
        Assert.All(cliente.PromptsConHerramientas, p => Assert.Contains("CONOCIMIENTO DE REFERENCIA", p));
        Assert.All(cliente.PromptsConHerramientas, p => Assert.Contains("Normativa", p));
        Assert.Equal([CerebroToolset.ToolListar, CerebroToolset.ToolBuscar, CerebroToolset.ToolVer], cliente.HerramientasOfrecidas);
        Assert.Contains(cliente.ResultadosHerramienta, r => r.Contains("Tabla de plazos"));
        Assert.DoesNotContain(cliente.ResultadosHerramienta, r => r.Contains("Contratos internos"));
        // El unificador solo junta textos: va sin herramientas.
        Assert.True(cliente.LlamadasSinHerramientas >= 1);

        await using var verif = Contexto(_tenant, _admin);
        var guardada = await verif.RespuestasTablaDocumental.FirstAsync(r => r.Id == respuestaId);
        Assert.False(string.IsNullOrWhiteSpace(guardada.Procedimiento));
    }

    [Fact]
    public async Task El_motor_de_IA_elegido_para_el_agente_es_el_que_genera()
    {
        var respuestaId = await CrearRespuestaAsync();
        await using (var ctx = Contexto(null, null))
        {
            ctx.AiProviderConfigs.Add(new AiProviderConfig { Provider = AiProvider.DeepSeek, IsEnabled = true, ApiKeyEncrypted = "clave-ds", Model = "deepseek-v4-flash" });
            ctx.AiProviderConfigs.Add(new AiProviderConfig { Provider = AiProvider.Gemini, IsEnabled = false, ApiKeyEncrypted = "clave-g" });
            await ctx.SaveChangesAsync();
        }

        // Primera generacion: siembra el agente con el primer motor habilitado (Claude).
        var cliente = new ClienteIaFalso();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            Assert.True((await Inferencia(ctx, cliente).GenerarProcedimientoAsync(respuestaId)).Ok);
        }
        Assert.All(cliente.Proveedores, p => Assert.Equal(AiProvider.Claude, p));

        Guid agenteId;
        await using (var ctx = Contexto(_tenant, _admin))
        {
            agenteId = (await ctx.AiAgents.FirstAsync(a => a.Role == ProcedimientoPrompts.AgenteRol)).Id;
            var svc = new AgenteCerebrosService(ctx, new ContextoPrueba(_tenant, _admin));
            var motores = await svc.MotoresAsync();
            Assert.Equal([AiProvider.Claude, AiProvider.DeepSeek], motores.Select(m => m.Provider).ToArray());
            Assert.Equal("deepseek-v4-flash", motores.Single(m => m.Provider == AiProvider.DeepSeek).Modelo);

            // Un motor deshabilitado no se puede elegir; un asesor no lo cambia.
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.FijarMotorAsync(agenteId, AiProvider.Gemini));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new AgenteCerebrosService(Contexto(_tenant, _asesor), new ContextoPrueba(_tenant, _asesor)).FijarMotorAsync(agenteId, AiProvider.DeepSeek));
            await svc.FijarMotorAsync(agenteId, AiProvider.DeepSeek);
        }

        var cliente2 = new ClienteIaFalso();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            Assert.True((await Inferencia(ctx, cliente2).GenerarProcedimientoAsync(respuestaId)).Ok);
            Assert.True((await Inferencia(ctx, cliente2).ConversarAsync(agenteId, [new AiChatTurn("user", "hola")])).Ok);
        }
        Assert.NotEmpty(cliente2.Proveedores);
        Assert.All(cliente2.Proveedores, p => Assert.Equal(AiProvider.DeepSeek, p));
        Assert.All(cliente2.Modelos, m => Assert.Equal("deepseek-v4-flash", m));
    }

    [Fact]
    public async Task Busqueda_ignora_tildes_y_plurales_y_lee_notas_largas_por_partes()
    {
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var svc = Cerebros(ctx);
            var id = (await svc.CrearAsync("Minas", null, null)).Id;
            await svc.GuardarNotaAsync(id, null, "Resoluci\u00f3n 00168 de 2026", "Ordena el giro de subsidios del FSSRI a CEDENAR por los trimestres de 2018.");
            await svc.GuardarNotaAsync(id, null, "Glosario", "Que es un subsidio y para que sirve.");
            var largo = new string('a', 13_000) + " La clave CONCILIACION FINAL esta aqui. " + new string('b', 14_000);
            await svc.GuardarNotaAsync(id, null, "Res_00169_2026-02-17", largo);
        }

        await using var ctxA = Contexto(_tenant, _admin);
        var tool = new CerebroToolset(ctxA);

        // Sin tilde, en plural y con palabras vacias ("que", "los"): igual la encuentra primero.
        var b = await tool.ExecuteAsync(CerebroToolset.ToolBuscar, """{"texto":"que resoluciones dan los subsidios a Cedenar"}""");
        using (var doc = JsonDocument.Parse(b.Json))
        {
            Assert.Equal("Resoluci\u00f3n 00168 de 2026", doc.RootElement.GetProperty("resultados")[0].GetProperty("titulo").GetString());
            Assert.DoesNotContain("que", doc.RootElement.GetProperty("buscado").EnumerateArray().Select(x => x.GetString()));
        }

        // Nota larga: por partes y con busqueda interna.
        var p1 = await tool.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"minas","titulo":"Res_00169_2026-02-17"}""");
        using (var doc = JsonDocument.Parse(p1.Json))
        {
            Assert.Equal(3, doc.RootElement.GetProperty("total_partes").GetInt32());
            Assert.DoesNotContain("CONCILIACION", doc.RootElement.GetProperty("contenido").GetString());
        }
        var p2 = await tool.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"minas","titulo":"Res_00169_2026-02-17","parte":2}""");
        Assert.Contains("CONCILIACION FINAL", p2.Json);
        var bus = await tool.ExecuteAsync(CerebroToolset.ToolVer, """{"cerebro":"minas","titulo":"Res_00169_2026-02-17","buscar":"conciliacion"}""");
        using (var doc = JsonDocument.Parse(bus.Json))
        {
            var pasajes = doc.RootElement.GetProperty("pasajes");
            Assert.Equal(1, pasajes.GetArrayLength());
            Assert.Contains("CONCILIACION FINAL", pasajes[0].GetProperty("texto").GetString());
            Assert.Equal(2, pasajes[0].GetProperty("parte").GetInt32());
        }
    }

    [Theory]
    [InlineData("profundo", 14)]
    [InlineData("rapido", 5)]
    public async Task Si_agota_las_consultas_el_agente_responde_con_lo_que_encontro(string modo, int rondas)
    {
        Guid agente;
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var manual = (await Cerebros(ctx).CrearAsync("Manual", null, null)).Id;
            await Cerebros(ctx).GuardarNotaAsync(manual, null, "Tabla de plazos", "Los contratos se conservan 20 anios.");
            await Cerebros(ctx).GuardarNotaAsync(manual, null, "Indice", "Ver [[Tabla de plazos]]");
            var a = new AiAgent { TenantId = _tenant, Name = "Asesor", Provider = AiProvider.Claude, SystemPrompt = "Ayuda." };
            ctx.AiAgents.Add(a);
            await ctx.SaveChangesAsync();
            agente = a.Id;
            var svc = new AgenteCerebrosService(ctx, new ContextoPrueba(_tenant, _admin));
            await svc.FijarAsync(agente, manual, true);
            await svc.FijarModoAsync(agente, modo);
            Assert.Equal(modo, await svc.ModoAsync(agente));
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.FijarModoAsync(agente, "turbo"));
        }

        var cliente = new ClienteIaInsaciable();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var res = await Inferencia(ctx, cliente).ConversarAsync(agente, [new AiChatTurn("user", "cuanto se conservan los contratos?")]);
            Assert.True(res.Ok, res.Error);
            Assert.Equal("Cierre: 20 anios segun Tabla de plazos.", res.Text);
            Assert.Contains(res.Fuentes!, f => f.Titulo == "Tabla de plazos");
        }
        Assert.Equal(rondas, cliente.RondasConHerramientas);
        // El cierre recibe lo consultado y la orden de responder sin herramientas.
        Assert.Contains("Los contratos se conservan 20 anios", cliente.CierrePrompt);
        Assert.Contains("Ya no puedes hacer mas consultas", cliente.CierrePrompt);
        // El prompt trae el mapa del cerebro y el modo.
        Assert.Contains("Puntos de entrada: Indice", cliente.SystemPrompt);
        Assert.Contains(modo == "profundo" ? "INVESTIGA A FONDO" : "MODO RAPIDO", cliente.SystemPrompt);
    }

    /// <summary>Modelo que nunca deja de pedir herramientas; solo responde cuando se le llama sin ellas.</summary>
    private sealed class ClienteIaInsaciable : IAiProviderClient
    {
        public int RondasConHerramientas { get; private set; }
        public string CierrePrompt { get; private set; } = "";
        public string SystemPrompt { get; private set; } = "";

        public Task<AiChatResult> CompleteAsync(AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt,
            IReadOnlyList<AiChatTurn> turns, CancellationToken cancellationToken = default)
        {
            CierrePrompt = turns[^1].Text;
            return Task.FromResult(new AiChatResult(true, "Cierre: 20 anios segun Tabla de plazos.", null, 10, 5));
        }

        public Task<AiCompletion> CompleteWithToolsAsync(AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt,
            IReadOnlyList<AiToolMessage> messages, IReadOnlyList<AiToolSpec> tools, CancellationToken cancellationToken = default)
        {
            SystemPrompt = systemPrompt;
            RondasConHerramientas++;
            var call = RondasConHerramientas % 2 == 1
                ? new AiToolCall("c" + RondasConHerramientas, CerebroToolset.ToolBuscar, """{"texto":"contratos plazos"}""")
                : new AiToolCall("c" + RondasConHerramientas, CerebroToolset.ToolVer, """{"cerebro":"manual","titulo":"Tabla de plazos"}""");
            return Task.FromResult(new AiCompletion(true, "", [call], null, 10, 5));
        }
    }

    [Fact]
    public async Task Sin_cerebros_de_referencia_el_procedimiento_no_usa_herramientas()
    {
        var respuestaId = await CrearRespuestaAsync();
        var cliente = new ClienteIaFalso();
        await using var ctx = Contexto(_tenant, _admin);
        var res = await Inferencia(ctx, cliente).GenerarProcedimientoAsync(respuestaId);
        Assert.True(res.Ok, res.Error);
        Assert.Empty(cliente.PromptsConHerramientas);
        Assert.True(cliente.LlamadasSinHerramientas >= 3);
    }

    [Fact]
    public async Task Chat_del_agente_usa_solo_las_herramientas_habilitadas()
    {
        Guid conHerramientas, sinHerramientas;
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var c = (await Cerebros(ctx).CrearAsync("Manual", null, null)).Id;
            await Cerebros(ctx).GuardarNotaAsync(c, null, "Radicacion", "Paso a paso de la radicacion de plazos");
            var a1 = new AiAgent { TenantId = _tenant, Name = "Con cerebros", Provider = AiProvider.Claude, SystemPrompt = "Ayuda.", ToolKeys = ToolsHelper.Serialize([CerebroToolset.ToolBuscar]) };
            var a2 = new AiAgent { TenantId = _tenant, Name = "Simple", Provider = AiProvider.Claude, SystemPrompt = "Ayuda." };
            ctx.AiAgents.AddRange(a1, a2);
            await ctx.SaveChangesAsync();
            (conHerramientas, sinHerramientas) = (a1.Id, a2.Id);
        }

        var cliente = new ClienteIaFalso();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var r = await Inferencia(ctx, cliente).TestChatAsync(conHerramientas, [new AiChatTurn("user", "Como radico?")]);
            Assert.True(r.Ok, r.Error);
        }
        // Solo la herramienta habilitada (no listar/ver ni la TRD).
        Assert.Equal([CerebroToolset.ToolBuscar], cliente.HerramientasOfrecidas);
        Assert.Contains(cliente.ResultadosHerramienta, x => x.Contains("Radicacion"));

        var cliente2 = new ClienteIaFalso();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            await Inferencia(ctx, cliente2).TestChatAsync(sinHerramientas, [new AiChatTurn("user", "Hola")]);
        }
        Assert.Empty(cliente2.PromptsConHerramientas);
        Assert.Equal(1, cliente2.LlamadasSinHerramientas);
    }

    [Fact]
    public async Task Chat_del_agente_consulta_solo_los_cerebros_que_tiene_asignados()
    {
        Guid agente, generacion, manual, secreto;
        await using (var ctx = Contexto(_tenant, _admin))
        {
            manual = (await Cerebros(ctx).CrearAsync("Manual de plazos", null, null)).Id;
            secreto = (await Cerebros(ctx).CrearAsync("Secreto", null, null)).Id;
            await Cerebros(ctx).GuardarNotaAsync(manual, null, "Tabla de plazos", "Los contratos de plazos se conservan 20 anios.");
            await Cerebros(ctx).GuardarNotaAsync(secreto, null, "Plazos ocultos", "plazos contratos radicacion confidencial");
            var a = new AiAgent { TenantId = _tenant, Name = "Asesor", Provider = AiProvider.Claude, SystemPrompt = "Ayuda." };
            var g = new AiAgent { TenantId = _tenant, Name = "Generacion", Role = ProcedimientoPrompts.AgenteRol, Provider = AiProvider.Claude, SystemPrompt = "Genera XML." };
            ctx.AiAgents.AddRange(a, g);
            await ctx.SaveChangesAsync();
            (agente, generacion) = (a.Id, g.Id);
        }

        // Solo el admin cambia la seleccion.
        await using (var ctx = Contexto(_tenant, _asesor))
        {
            var svc = new AgenteCerebrosService(ctx, new ContextoPrueba(_tenant, _asesor));
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.FijarAsync(agente, manual, true));
        }
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var svc = new AgenteCerebrosService(ctx, new ContextoPrueba(_tenant, _admin));
            await svc.FijarAsync(agente, manual, true);
            // En el agente de generacion la seleccion ES la marca UsoIa del cerebro.
            await svc.FijarAsync(generacion, secreto, true);
        }
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var svc = new AgenteCerebrosService(ctx, new ContextoPrueba(_tenant, _admin));
            var vista = await svc.CerebrosAsync(agente);
            Assert.True(vista.Single(c => c.Id == manual).Seleccionado);
            Assert.False(vista.Single(c => c.Id == secreto).Seleccionado);
            Assert.True(await ctx.Cerebros.Where(c => c.Id == secreto).Select(c => c.UsoIa).SingleAsync());
            Assert.Equal([secreto], await svc.SeleccionadosAsync(generacion));
        }

        // El chat ofrece las herramientas de cerebros acotadas: encuentra Manual, nunca Secreto.
        var cliente = new ClienteIaFalso();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var r = await Inferencia(ctx, cliente).ConversarAsync(agente, [new AiChatTurn("user", "Cuanto se conservan los contratos?")]);
            Assert.True(r.Ok, r.Error);
            // Fuentes reales: el falso solo busca (no abre notas), asi que todo es "vista en busqueda".
            Assert.NotNull(r.Fuentes);
            Assert.Contains(r.Fuentes!, f => f.Titulo == "Tabla de plazos" && !f.Leida && f.CerebroNombre == "Manual de plazos");
            Assert.DoesNotContain(r.Fuentes!, f => f.Titulo == "Plazos ocultos");
        }
        Assert.Contains(CerebroToolset.ToolBuscar, cliente.HerramientasOfrecidas);
        Assert.Contains("Manual de plazos", cliente.PromptsConHerramientas.Single());
        Assert.Contains("MODO CONVERSACION", cliente.PromptsConHerramientas.Single());
        Assert.Contains(cliente.ResultadosHerramienta, x => x.Contains("Tabla de plazos"));
        Assert.DoesNotContain(cliente.ResultadosHerramienta, x => x.Contains("Plazos ocultos"));

        // Sin cerebros asignados: conversacion simple, sin herramientas.
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var svc = new AgenteCerebrosService(ctx, new ContextoPrueba(_tenant, _admin));
            await svc.FijarAsync(agente, manual, false);
        }
        var cliente2 = new ClienteIaFalso();
        await using (var ctx = Contexto(_tenant, _admin))
        {
            var r2 = await Inferencia(ctx, cliente2).ConversarAsync(agente, [new AiChatTurn("user", "Hola")]);
            Assert.Null(r2.Fuentes);
        }
        Assert.Empty(cliente2.PromptsConHerramientas);
        Assert.Equal(1, cliente2.LlamadasSinHerramientas);
    }

    // ---------------- helpers ----------------

    private async Task<Guid> CrearRespuestaAsync()
    {
        await using var ctx = Contexto(_tenant, _admin);
        var admin = new TrdAdminService(ctx, new ContextoPrueba(_tenant, _admin), TimeProvider.System, new HasherNulo());
        var trd = new TablaRetencionDocumental { TenantId = _tenant, Consecutivo = $"TRD-{Guid.NewGuid():N}"[..12], Titulo = "Prueba", Estado = "ACTIVO" };
        ctx.TablasRetencionDocumental.Add(trd);
        await ctx.SaveChangesAsync();
        var dep = await admin.AgregarDependenciaAsync(new CrearDependenciaRequest { TrdId = trd.Id, Codigo = "100", NombreCargo = "Juridica" }, Guid.Empty);
        var serie = new Serie { TenantId = _tenant, Codigo = $"S{Guid.NewGuid():N}"[..8], Nombre = "Contratos" };
        ctx.Series.Add(serie);
        var r = new RespuestaTablaDocumental
        {
            TenantId = _tenant, TrdId = trd.Id, DependenciaId = dep!.Id, SerieId = serie.Id,
            TiempoAg = 2, TiempoAc = 18, DispE = true, Extension = "{}", FechaReg = DateTimeOffset.UtcNow, CreadoPor = Guid.Empty
        };
        ctx.RespuestasTablaDocumental.Add(r);
        await ctx.SaveChangesAsync();
        return r.Id;
    }

    private static CerebroService Cerebros(DokTrinoDbContext ctx) =>
        new(ctx, ctx.TenantContextoParaPruebas(), TimeProvider.System);

    private static AiInferenceService Inferencia(DokTrinoDbContext ctx, IAiProviderClient cliente) =>
        new(ctx, new ProtectorNulo(), cliente, new ConsumoNulo(), [new TrdLecturaToolset(ctx), new CerebroToolset(ctx)]);

    private DokTrinoDbContext Contexto(Guid? tenantId, Guid? userId)
    {
        var tc = new ContextoPrueba(tenantId, userId);
        var options = new DbContextOptionsBuilder<DokTrinoDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableTenantInterceptor(tc, TimeProvider.System))
            .Options;
        var ctx = new DokTrinoDbContext(options, tc);
        ContextosDePrueba.Registrar(ctx, tc);
        return ctx;
    }

    private sealed class ContextoPrueba(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public Guid? UserId { get; } = userId;
    }

    private sealed class HasherNulo : IPasswordHasher
    {
        public string Hash(string password) => "x";
        public bool Verify(string hash, string password) => false;
    }

    private sealed class ProtectorNulo : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }

    private sealed class ConsumoNulo : IAiUsageService
    {
        public Task RecordAsync(Guid? agentId, AiProvider provider, string model, int inputTokens, int outputTokens, string source, bool success, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiUsageSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiQuotaDto> GetQuotaAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiQuotaDto(0, 0, false));
    }

    /// <summary>
    /// Proveedor falso: con herramientas, la primera ronda pide buscar_notas y la segunda
    /// responde con el XML segun lo que devolvio la herramienta. Sin herramientas, responde XML.
    /// </summary>
    private sealed class ClienteIaFalso : IAiProviderClient
    {
        public List<string> PromptsConHerramientas { get; } = new();
        public string[] HerramientasOfrecidas { get; private set; } = [];
        public List<string> ResultadosHerramienta { get; } = new();
        public int LlamadasSinHerramientas { get; private set; }
        public List<AiProvider> Proveedores { get; } = new();
        public List<string> Modelos { get; } = new();

        public Task<AiChatResult> CompleteAsync(AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt,
            IReadOnlyList<AiChatTurn> turns, CancellationToken cancellationToken = default)
        {
            Proveedores.Add(provider);
            Modelos.Add(model);
            LlamadasSinHerramientas++;
            return Task.FromResult(new AiChatResult(true, "<PROCEDIMIENTO>Texto sin consulta.</PROCEDIMIENTO>", null, 10, 5));
        }

        public Task<AiCompletion> CompleteWithToolsAsync(AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt,
            IReadOnlyList<AiToolMessage> messages, IReadOnlyList<AiToolSpec> tools, CancellationToken cancellationToken = default)
        {
            Proveedores.Add(provider);
            Modelos.Add(model);
            HerramientasOfrecidas = tools.Select(t => t.Name).ToArray();
            var resultado = messages.LastOrDefault(m => m.Role == "tool");
            if (resultado is null)
            {
                PromptsConHerramientas.Add(systemPrompt);
                return Task.FromResult(new AiCompletion(true, null,
                    [new AiToolCall("c1", CerebroToolset.ToolBuscar, """{"texto":"plazos contratos radicacion"}""")], null, 10, 5));
            }
            ResultadosHerramienta.Add(resultado.Text ?? "");
            var dato = (resultado.Text ?? "").Contains("Tabla de plazos") ? "PLAZOS" : "SIN DATO";
            return Task.FromResult(new AiCompletion(true, $"<PROCEDIMIENTO>Segun el cerebro: {dato}.</PROCEDIMIENTO>", [], null, 10, 5));
        }
    }
}

/// <summary>Permite recuperar el ITenantContext con que se creo un DbContext de prueba.</summary>
internal static class ContextosDePrueba
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DokTrinoDbContext, ITenantContext> Mapa = new();

    public static void Registrar(DokTrinoDbContext ctx, ITenantContext tc) => Mapa.AddOrUpdate(ctx, tc);

    public static ITenantContext TenantContextoParaPruebas(this DokTrinoDbContext ctx) =>
        Mapa.TryGetValue(ctx, out var tc) ? tc : throw new InvalidOperationException("Contexto de prueba sin registrar.");
}
