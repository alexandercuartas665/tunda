using System.Reflection;
using DokTrino.Application.Common;
using DokTrino.Domain.Common;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Infrastructure.Persistence;

public class DokTrinoDbContext : DbContext, IApplicationDbContext, IDataProtectionKeyContext
{
    private readonly ITenantContext _tenantContext;

    public DokTrinoDbContext(DbContextOptions<DokTrinoDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Globales (administradas por Super Admin / plataforma)
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SaasPlan> SaasPlans => Set<SaasPlan>();
    public DbSet<SaasPlanLimit> SaasPlanLimits => Set<SaasPlanLimit>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantPayment> TenantPayments => Set<TenantPayment>();
    public DbSet<WompiMasterConfig> WompiMasterConfigs => Set<WompiMasterConfig>();
    public DbSet<WompiWebhookEvent> WompiWebhookEvents => Set<WompiWebhookEvent>();
    public DbSet<EvolutionMasterConfig> EvolutionMasterConfigs => Set<EvolutionMasterConfig>();
    public DbSet<AiProviderConfig> AiProviderConfigs => Set<AiProviderConfig>();
    public DbSet<PlatformBranding> PlatformBrandings => Set<PlatformBranding>();
    public DbSet<EmailConfig> EmailConfigs => Set<EmailConfig>();
    public DbSet<GoogleAuthConfig> GoogleAuthConfigs => Set<GoogleAuthConfig>();
    public DbSet<TenantApiConfig> TenantApiConfigs => Set<TenantApiConfig>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<SuperAdminAuditLog> SuperAdminAuditLogs => Set<SuperAdminAuditLog>();

    // Llaves de Data Protection compartidas entre apps (Api, SuperAdmin, Workers) para
    // que los secretos cifrados (Wompi, Evolution) se descifren en cualquiera de ellas.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Tenant-scoped (con filtro global de consulta)
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<TenantConfiguration> TenantConfigurations => Set<TenantConfiguration>();
    public DbSet<TenantEvolutionConfig> TenantEvolutionConfigs => Set<TenantEvolutionConfig>();
    public DbSet<WhatsAppLine> WhatsAppLines => Set<WhatsAppLine>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<PipelineFieldDefinition> PipelineFieldDefinitions => Set<PipelineFieldDefinition>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();
    public DbSet<LeadNote> LeadNotes => Set<LeadNote>();
    public DbSet<LeadFile> LeadFiles => Set<LeadFile>();
    public DbSet<FollowUpTask> FollowUpTasks => Set<FollowUpTask>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<QuoteTemplate> QuoteTemplates => Set<QuoteTemplate>();
    public DbSet<TemplateAsset> TemplateAssets => Set<TemplateAsset>();
    public DbSet<AiAgent> AiAgents => Set<AiAgent>();
    public DbSet<AiAgentResource> AiAgentResources => Set<AiAgentResource>();
    public DbSet<AiAgentPrompt> AiAgentPrompts => Set<AiAgentPrompt>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FormDefinitionSnapshot> FormDefinitionSnapshots => Set<FormDefinitionSnapshot>();
    public DbSet<SqlConsoleLog> SqlConsoleLogs => Set<SqlConsoleLog>();
    public DbSet<RelacionFormulario> RelacionesFormulario => Set<RelacionFormulario>();
    public DbSet<TipologiaArchivo> TipologiaArchivos => Set<TipologiaArchivo>();
    public DbSet<TipoProfesional> TiposProfesional => Set<TipoProfesional>();
    public DbSet<SubCategoriaProfesional> SubCategoriasProfesional => Set<SubCategoriaProfesional>();
    public DbSet<Profesional> Profesionales => Set<Profesional>();
    public DbSet<ProfesionalSubCategoria> ProfesionalSubCategorias => Set<ProfesionalSubCategoria>();
    public DbSet<ProfesionalAgencia> ProfesionalAgencias => Set<ProfesionalAgencia>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<TenantUserSucursal> TenantUserSucursales => Set<TenantUserSucursal>();
    public DbSet<Pais> Paises => Set<Pais>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Municipio> Municipios => Set<Municipio>();

    // ----- Documental: TRD (2.D1 admin + 2.D2 cliente, segun spec) -----
    public DbSet<Segmento> Segmentos => Set<Segmento>();
    public DbSet<TablaRetencionDocumental> TablasRetencionDocumental => Set<TablaRetencionDocumental>();
    public DbSet<Dependencia> Dependencias => Set<Dependencia>();
    public DbSet<Serie> Series => Set<Serie>();
    public DbSet<Subserie> Subseries => Set<Subserie>();
    public DbSet<TipologiaDocumental> TipologiasDocumentales => Set<TipologiaDocumental>();
    public DbSet<TokenDependencia> TokensDependencia => Set<TokenDependencia>();
    public DbSet<RespuestaTablaDocumental> RespuestasTablaDocumental => Set<RespuestaTablaDocumental>();
    public DbSet<FormatoSerie> FormatosSerie => Set<FormatoSerie>();
    public DbSet<CargoSerie> CargosSerie => Set<CargoSerie>();
    public DbSet<FuncionarioCargo> FuncionariosCargo => Set<FuncionarioCargo>();
    public DbSet<DirectorioSerie> DirectoriosSerie => Set<DirectorioSerie>();
    public DbSet<Expediente> Expedientes => Set<Expediente>();
    public DbSet<CuestionarioCapacitacion> Cuestionarios => Set<CuestionarioCapacitacion>();
    public DbSet<CuestionarioPregunta> CuestionarioPreguntas => Set<CuestionarioPregunta>();
    public DbSet<CuestionarioIntento> CuestionarioIntentos => Set<CuestionarioIntento>();
    public DbSet<ProcesoNodo> ProcesoNodos => Set<ProcesoNodo>();
    public DbSet<ProcesoTransicion> ProcesoTransiciones => Set<ProcesoTransicion>();
    public DbSet<NivelTopografico> NivelesTopograficos => Set<NivelTopografico>();
    public DbSet<ElementoTopografico> ElementosTopograficos => Set<ElementoTopografico>();
    public DbSet<Complemento> Complementos => Set<Complemento>();
    public DbSet<CatalogoCaracteristica> CatalogoCaracteristicas => Set<CatalogoCaracteristica>();
    public DbSet<ColaboradorDependencia> ColaboradoresDependencia => Set<ColaboradorDependencia>();
    public DbSet<FormacionDependencia> FormacionesDependencia => Set<FormacionDependencia>();
    public DbSet<Radicado> Radicados => Set<Radicado>();
    public DbSet<Bodega> Bodegas => Set<Bodega>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<Carpeta> Carpetas => Set<Carpeta>();
    public DbSet<ArchivoDigital> ArchivosDigitales => Set<ArchivoDigital>();
    public DbSet<CarpetaArchivo> CarpetasArchivo => Set<CarpetaArchivo>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArchivoTag> ArchivoTags => Set<ArchivoTag>();
    public DbSet<AprobacionDocumento> AprobacionesDocumento => Set<AprobacionDocumento>();

    // ----- BPMN / Procesos -----
    public DbSet<ProcesoDefinicion> ProcesosDefinicion => Set<ProcesoDefinicion>();
    public DbSet<ProcesoActividad> ProcesoActividades => Set<ProcesoActividad>();
    public DbSet<ProcesoInstancia> ProcesoInstancias => Set<ProcesoInstancia>();
    public DbSet<Tarea> Tareas => Set<Tarea>();

    // ----- 2.D5 Power BI Servicios -----
    public DbSet<BiServicio> BiServicios => Set<BiServicio>();
    public DbSet<BiTokenUso> BiTokensUso => Set<BiTokenUso>();
    public DbSet<BiLog> BiLogs => Set<BiLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Todos los enums se persisten como texto (legibles y estables ante reordenamientos).
        configurationBuilder.Properties<TenantStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TenantKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<SubscriptionStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<BillingFrequency>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PlatformRole>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<LimitEnforcementMode>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AuditActorType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TenantRole>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PlatformUserStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<LeadVisibility>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WhatsAppLineStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<LeadStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FollowUpTaskStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<MessageDirection>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<MessageMediaType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AiProvider>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AgentResourceType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AutomationTrigger>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AutomationAction>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WompiEnvironment>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WompiIntegrationStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<EvolutionIntegrationStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WebhookProcessingStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PipelineFieldType>().HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureEntities(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.LegalName).HasMaxLength(250);
            b.Property(x => x.TaxId).HasMaxLength(80);
            b.Property(x => x.Country).HasMaxLength(80);
            b.Property(x => x.Currency).HasMaxLength(10);
            b.Property(x => x.LogoUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<SaasPlan>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Currency).HasMaxLength(10);
            b.Property(x => x.MonthlyPrice).HasPrecision(12, 2);
            b.Property(x => x.YearlyPrice).HasPrecision(12, 2);
            b.HasMany(x => x.Limits)
                .WithOne(x => x.Plan!)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SaasPlanLimit>(b =>
        {
            b.Property(x => x.LimitKey).HasMaxLength(150).IsRequired();
            b.Property(x => x.LimitUnit).HasMaxLength(50);
            b.HasIndex(x => new { x.PlanId, x.LimitKey }).IsUnique();
        });

        modelBuilder.Entity<TenantSubscription>(b =>
        {
            b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.PaymentMethodLabel).HasMaxLength(80);
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<TenantPayment>(b =>
        {
            b.Property(x => x.Amount).HasPrecision(12, 2);
            b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            b.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProviderReference).HasMaxLength(200);
            b.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<PlatformUser>(b =>
        {
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200);
            b.Property(x => x.GoogleSubject).HasMaxLength(255);
            b.Property(x => x.AuthProvider).HasMaxLength(50).IsRequired();
            b.Property(x => x.Documento).HasMaxLength(40);
            // Campos personales (modulo Administracion de Usuarios)
            b.Property(x => x.Username).HasMaxLength(80);
            b.Property(x => x.PrimerNombre).HasMaxLength(80);
            b.Property(x => x.SegundoNombre).HasMaxLength(80);
            b.Property(x => x.PrimerApellido).HasMaxLength(80);
            b.Property(x => x.SegundoApellido).HasMaxLength(80);
            b.Property(x => x.Celular).HasMaxLength(40);
            b.Property(x => x.Fijo).HasMaxLength(40);
            b.Property(x => x.Ciudad).HasMaxLength(120);
            b.Property(x => x.Direccion).HasMaxLength(250);
            b.HasIndex(x => x.Email).IsUnique();
            b.HasIndex(x => x.GoogleSubject).IsUnique().HasFilter("google_subject IS NOT NULL");
            b.HasIndex(x => x.Documento).IsUnique().HasFilter("documento IS NOT NULL");
            // Username unico (excluye nulls para no chocar entre usuarios sin username).
            b.HasIndex(x => x.Username).IsUnique().HasFilter("username IS NOT NULL");
        });

        modelBuilder.Entity<SuperAdminAuditLog>(b =>
        {
            b.Property(x => x.ActionName).HasMaxLength(200).IsRequired();
            b.Property(x => x.EntityName).HasMaxLength(150).IsRequired();
            b.Property(x => x.IpAddress).HasMaxLength(80);
            b.Property(x => x.PreviousValue).HasColumnType("jsonb");
            b.Property(x => x.NewValue).HasColumnType("jsonb");
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<PlatformBranding>(b =>
        {
            b.Property(x => x.PlatformName).HasMaxLength(120).IsRequired();
            b.Property(x => x.Tagline).HasMaxLength(160);
            b.Property(x => x.LoginLogoUrl).HasMaxLength(500);
            b.Property(x => x.LoginHeadline).HasMaxLength(160);
            b.Property(x => x.LoginSubtext).HasMaxLength(600);
        });

        modelBuilder.Entity<EmailConfig>(b =>
        {
            b.Property(x => x.SmtpHost).HasMaxLength(200);
            b.Property(x => x.SmtpUser).HasMaxLength(200);
            b.Property(x => x.FromEmail).HasMaxLength(200);
            b.Property(x => x.FromName).HasMaxLength(160);
        });

        modelBuilder.Entity<PasswordResetToken>(b =>
        {
            b.Property(x => x.TokenHash).HasMaxLength(80).IsRequired();
            b.HasIndex(x => x.TokenHash);
            b.HasIndex(x => x.PlatformUserId);
        });

        modelBuilder.Entity<GoogleAuthConfig>(b =>
        {
            b.Property(x => x.ClientId).HasMaxLength(300);
        });

        modelBuilder.Entity<TenantApiConfig>(b =>
        {
            b.Property(x => x.ApiKeyHash).HasMaxLength(80);
            b.HasIndex(x => x.ApiKeyHash);
            b.HasIndex(x => x.TenantId).IsUnique();
        });

        modelBuilder.Entity<WompiMasterConfig>(b =>
        {
            b.Property(x => x.PublicKey).HasMaxLength(200);
            b.Property(x => x.WebhookEndpoint).HasMaxLength(500);
            b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        });

        modelBuilder.Entity<EvolutionMasterConfig>(b =>
        {
            b.Property(x => x.BaseUrl).HasMaxLength(500);
            b.Property(x => x.WebhookMode).HasMaxLength(20).HasDefaultValue("Development");
            b.Property(x => x.WebhookPublicUrl).HasMaxLength(500);
            b.Property(x => x.WebhookActiveUrl).HasMaxLength(500);
            b.Property(x => x.WebhookToken).HasMaxLength(200);
        });

        modelBuilder.Entity<AiProviderConfig>(b =>
        {
            b.Property(x => x.Model).HasMaxLength(120);
            b.Property(x => x.BaseUrl).HasMaxLength(500);
            b.HasIndex(x => x.Provider).IsUnique();
        });

        modelBuilder.Entity<WompiWebhookEvent>(b =>
        {
            b.Property(x => x.ProviderEventId).HasMaxLength(250).IsRequired();
            b.Property(x => x.TransactionId).HasMaxLength(200);
            b.Property(x => x.Reference).HasMaxLength(200);
            b.Property(x => x.Note).HasMaxLength(500);
            b.Property(x => x.RawPayload).HasColumnType("jsonb");
            // Idempotencia: un evento (transaction + timestamp) no se procesa dos veces.
            b.HasIndex(x => x.ProviderEventId).IsUnique();
        });

        modelBuilder.Entity<TenantUser>(b =>
        {
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.InvitationToken).HasMaxLength(128);
            b.HasOne(x => x.PlatformUser).WithMany().HasForeignKey(x => x.PlatformUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.PlatformUserId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            b.HasIndex(x => x.InvitationToken);
        });

        modelBuilder.Entity<TenantConfiguration>(b =>
        {
            b.Property(x => x.ConfigKey).HasMaxLength(150).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.ConfigKey }).IsUnique();
        });

        modelBuilder.Entity<TenantEvolutionConfig>(b =>
        {
            // Campos del servidor propio: opcionales (cuando la agencia usa el servidor maestro quedan nulos).
            b.Property(x => x.BaseUrl).HasMaxLength(500);
            b.Property(x => x.InstanceName).HasMaxLength(200);
            b.Property(x => x.WebhookUrl).HasMaxLength(500);
            // Una configuracion Evolution por tenant.
            b.HasIndex(x => x.TenantId).IsUnique();
        });

        modelBuilder.Entity<WhatsAppLine>(b =>
        {
            b.Property(x => x.InstanceName).HasMaxLength(200).IsRequired();
            b.Property(x => x.PhoneNumber).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.InstanceName }).IsUnique();
            b.HasIndex(x => x.AssignedToTenantUserId);
        });

        modelBuilder.Entity<PipelineStage>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.SortOrder });
        });

        modelBuilder.Entity<PipelineFieldDefinition>(b =>
        {
            b.Property(x => x.FieldKey).HasMaxLength(80).IsRequired();
            b.Property(x => x.Label).HasMaxLength(150).IsRequired();
            b.Property(x => x.Options).HasMaxLength(2000);
            b.Property(x => x.Description).HasMaxLength(600);
            b.Property(x => x.RepeatWithFieldKey).HasMaxLength(80);
            b.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.StageId, x.SortOrder });
            b.HasIndex(x => new { x.StageId, x.FieldKey }).IsUnique();
        });

        modelBuilder.Entity<Lead>(b =>
        {
            b.Property(x => x.ContactName).HasMaxLength(200).IsRequired();
            b.Property(x => x.ContactPhone).HasMaxLength(40);
            b.Property(x => x.Destination).HasMaxLength(200);
            b.Property(x => x.Currency).HasMaxLength(10);
            b.Property(x => x.LossReason).HasMaxLength(500);
            b.Property(x => x.EstimatedValue).HasPrecision(14, 2);
            b.Property(x => x.FieldValuesJson).HasColumnType("jsonb");
            b.Property(x => x.ArchiveReason).HasMaxLength(80);
            b.Property(x => x.ArchiveNote).HasMaxLength(1000);
            b.Property(x => x.ArchivedByName).HasMaxLength(200);
            b.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.StageId });
            b.HasIndex(x => x.AssignedToTenantUserId);
            b.HasIndex(x => new { x.TenantId, x.ArchivedAt });
        });

        modelBuilder.Entity<LeadActivity>(b =>
        {
            b.Property(x => x.ActivityType).HasMaxLength(80).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000);
            b.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.LeadId });
        });

        modelBuilder.Entity<LeadNote>(b =>
        {
            b.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            b.Property(x => x.Color).HasMaxLength(20).IsRequired();
            b.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.LeadId });
        });

        modelBuilder.Entity<LeadFile>(b =>
        {
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.Url).HasMaxLength(500).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
            b.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.LeadId });
        });

        modelBuilder.Entity<FollowUpTask>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasIndex(x => new { x.TenantId, x.DueAt });
        });

        modelBuilder.Entity<Conversation>(b =>
        {
            b.Property(x => x.ContactPhone).HasMaxLength(40).IsRequired();
            b.Property(x => x.ContactName).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.ContactPhone }).IsUnique();
        });

        modelBuilder.Entity<Message>(b =>
        {
            b.Property(x => x.Body).HasMaxLength(4000);
            b.Property(x => x.MessageType).HasMaxLength(40).IsRequired();
            b.Property(x => x.ExternalId).HasMaxLength(200);
            b.Property(x => x.MediaUrl).HasMaxLength(500);
            b.Property(x => x.MediaMimeType).HasMaxLength(120);
            b.Property(x => x.SentByName).HasMaxLength(200);
            b.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ConversationId });
            // Idempotencia de ingesta: un mensaje externo no se inserta dos veces.
            b.HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique().HasFilter("external_id IS NOT NULL");
        });

        modelBuilder.Entity<MessageTemplate>(b =>
        {
            b.Property(x => x.Category).HasMaxLength(40).IsRequired();
            b.Property(x => x.Body).HasMaxLength(4000);
            b.Property(x => x.MediaUrl).HasMaxLength(500);
            b.Property(x => x.MediaMimeType).HasMaxLength(120);
            b.HasIndex(x => new { x.TenantId, x.Category, x.SortOrder });
        });

        modelBuilder.Entity<QuoteTemplate>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.HtmlContent).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.IsDefault });
        });

        modelBuilder.Entity<TemplateAsset>(b =>
        {
            b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            b.Property(x => x.Url).HasMaxLength(500).IsRequired();
            b.Property(x => x.MimeType).HasMaxLength(120);
            b.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        modelBuilder.Entity<AiAgent>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Role).HasMaxLength(100);
            b.Property(x => x.Model).HasMaxLength(100);
            b.Property(x => x.SystemPrompt).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.SortOrder });
        });

        modelBuilder.Entity<AiAgentResource>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Detail).HasColumnType("text");
            b.Property(x => x.FileUrl).HasMaxLength(500);
            b.Property(x => x.FileName).HasMaxLength(255);
            b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.AgentId, x.SortOrder });
        });

        modelBuilder.Entity<AiAgentPrompt>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Rule).HasMaxLength(500);
            b.Property(x => x.Body).HasColumnType("text");
            b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.AgentId, x.SortOrder });
        });

        modelBuilder.Entity<AiUsageLog>(b =>
        {
            b.Property(x => x.Model).HasMaxLength(120);
            b.Property(x => x.Source).HasMaxLength(40);
            b.Property(x => x.EstimatedCostUsd).HasPrecision(12, 6);
            b.HasIndex(x => new { x.TenantId, x.AgentId });
            b.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        modelBuilder.Entity<AutomationRule>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.FollowUpTitle).HasMaxLength(200);
            b.Property(x => x.TimeWindowStart).HasMaxLength(5);
            b.Property(x => x.TimeWindowEnd).HasMaxLength(5);
            b.Property(x => x.TemplateCategory).HasMaxLength(40);
            b.Property(x => x.ShiftName).HasMaxLength(60);
            b.HasOne(x => x.AiAgent).WithMany().HasForeignKey(x => x.AiAgentId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.SortOrder });
            b.HasIndex(x => x.AiAgentId);
        });

        modelBuilder.Entity<FormDefinition>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            b.Property(x => x.CodigoSecundario).HasMaxLength(80);
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasMaxLength(20);
            b.Property(x => x.Tipo).HasMaxLength(40);
            b.Property(x => x.SchemaJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.PrefillRoutesJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
            // CodigoSecundario NO es unico - es un id alternativo libre.
            b.HasIndex(x => new { x.TenantId, x.CodigoSecundario });
        });

        modelBuilder.Entity<FormDefinitionSnapshot>(b =>
        {
            // Misma forma que FormDefinition para que el snapshot pueda contener
            // cualquier valor que aceptaba la fila viva. MaxLength espejado.
            b.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasMaxLength(20);
            b.Property(x => x.Tipo).HasMaxLength(40);
            b.Property(x => x.SchemaJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.PrefillRoutesJson).HasColumnType("jsonb");
            b.Property(x => x.Motivo).HasMaxLength(80);
            b.HasOne(x => x.FormDefinition)
                .WithMany()
                .HasForeignKey(x => x.FormDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            // Index compuesto para el listado descendente por FormDefinition
            // (la consulta dominante: "ultimas 20 versiones de este formulario").
            b.HasIndex(x => new { x.FormDefinitionId, x.SnapshotAt })
                .IsDescending(false, true);
        });







        modelBuilder.Entity<SqlConsoleLog>(b =>
        {
            b.Property(x => x.Query).HasColumnType("text").IsRequired();
            b.Property(x => x.QueryType).HasMaxLength(20);
            b.Property(x => x.UserName).HasMaxLength(200);
            b.Property(x => x.ErrorMessage).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.ExecutedAt });
            b.HasIndex(x => x.ExecutedAt);
        });




        modelBuilder.Entity<RelacionFormulario>(b =>
        {
            b.Property(x => x.TipoRelacion).HasMaxLength(40);
            b.Property(x => x.Observacion).HasMaxLength(500);
            b.HasOne(x => x.FormularioOrigen).WithMany().HasForeignKey(x => x.FormularioOrigenId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.FormularioDestino).WithMany().HasForeignKey(x => x.FormularioDestinoId)
                .OnDelete(DeleteBehavior.Restrict);
            // No se permite el mismo par (origen, destino) duplicado dentro del tenant.
            b.HasIndex(x => new { x.TenantId, x.FormularioOrigenId, x.FormularioDestinoId }).IsUnique();
        });






        modelBuilder.Entity<TipologiaArchivo>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            b.Property(x => x.Color).HasMaxLength(20).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Activo });
        });




        modelBuilder.Entity<TipoProfesional>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
        });

        modelBuilder.Entity<SubCategoriaProfesional>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
        });

        modelBuilder.Entity<Profesional>(b =>
        {
            b.Property(x => x.NumeroDocumento).HasMaxLength(30).IsRequired();
            b.Property(x => x.TipoDocumento).HasMaxLength(10).IsRequired();
            b.Property(x => x.PrimerNombre).HasMaxLength(80);
            b.Property(x => x.SegundoNombre).HasMaxLength(80);
            b.Property(x => x.PrimerApellido).HasMaxLength(80);
            b.Property(x => x.SegundoApellido).HasMaxLength(80);
            b.Property(x => x.NombreCompleto).HasMaxLength(250).IsRequired();
            b.Property(x => x.RegistroMedico).HasMaxLength(60);
            b.Property(x => x.Ciudad).HasMaxLength(120);
            b.Property(x => x.Celular).HasMaxLength(40);
            b.Property(x => x.FirmaUrl).HasMaxLength(500);
            b.HasOne(x => x.TipoProfesional).WithMany().HasForeignKey(x => x.TipoProfesionalId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.NumeroDocumento }).IsUnique();
        });

        modelBuilder.Entity<ProfesionalSubCategoria>(b =>
        {
            b.HasOne(x => x.Profesional).WithMany().HasForeignKey(x => x.ProfesionalId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.SubCategoria).WithMany().HasForeignKey(x => x.SubCategoriaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ProfesionalId });
        });

        modelBuilder.Entity<ProfesionalAgencia>(b =>
        {
            b.Property(x => x.Agencia).HasMaxLength(120).IsRequired();
            b.HasOne(x => x.Profesional).WithMany().HasForeignKey(x => x.ProfesionalId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ProfesionalId });
        });


        modelBuilder.Entity<Rol>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            b.Property(x => x.Descripcion).HasMaxLength(400);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
        });

        modelBuilder.Entity<RolPermiso>(b =>
        {
            b.Property(x => x.Modulo).HasMaxLength(60).IsRequired();
            b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.RolId, x.Modulo }).IsUnique();
        });

        modelBuilder.Entity<Sucursal>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.Property(x => x.Direccion).HasMaxLength(300);
            b.Property(x => x.Ciudad).HasMaxLength(120);
            b.Property(x => x.Telefono).HasMaxLength(40);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        });



        modelBuilder.Entity<TenantUser>(b =>
        {
            b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.SetNull);
            b.HasMany(x => x.Sucursales).WithOne(x => x.TenantUser!).HasForeignKey(x => x.TenantUserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Profesional).WithMany().HasForeignKey(x => x.ProfesionalId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.ProfesionalId).IsUnique().HasFilter("profesional_id IS NOT NULL");
        });

        modelBuilder.Entity<TenantUserSucursal>(b =>
        {
            b.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantUserId, x.SucursalId }).IsUnique();
        });





        modelBuilder.Entity<Pais>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            b.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<Departamento>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            b.HasOne(x => x.Pais).WithMany().HasForeignKey(x => x.PaisId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.PaisId, x.Nombre }).IsUnique();
            b.HasIndex(x => new { x.PaisId, x.ExternalId });
        });

        modelBuilder.Entity<Municipio>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            b.HasOne(x => x.Departamento).WithMany().HasForeignKey(x => x.DepartamentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.DepartamentoId, x.Nombre }).IsUnique();
        });

        // ----- Documental: TRD segun spec 2.D1 / 2.D2 -----
        modelBuilder.Entity<Segmento>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<TablaRetencionDocumental>(b =>
        {
            b.Property(x => x.Consecutivo).HasMaxLength(20).IsRequired();
            b.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
            b.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            b.Property(x => x.Observaciones).HasColumnType("text");
            b.HasOne(x => x.Segmento).WithMany().HasForeignKey(x => x.SegmentoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.Consecutivo }).IsUnique();
        });

        modelBuilder.Entity<Dependencia>(b =>
        {
            b.Property(x => x.NombreCargo).HasMaxLength(200).IsRequired();
            b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            b.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            b.HasOne(x => x.Trd).WithMany(t => t.Dependencias).HasForeignKey(x => x.TrdId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Padre).WithMany(p => p.Hijos).HasForeignKey(x => x.PadreId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TrdId, x.PadreId, x.Orden }).IsUnique();
        });

        modelBuilder.Entity<Serie>(b =>
        {
            b.Property(x => x.DescripcionTiempo).HasColumnType("text");
            b.Property(x => x.DescripcionDisposicion).HasColumnType("text");
            b.Property(x => x.TiempoAg).HasPrecision(5, 2);
            b.Property(x => x.TiempoAc).HasPrecision(5, 2);
            b.Property(x => x.Procedimiento).HasColumnType("text");
            b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.Property(x => x.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("MAESTRA");
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<Subserie>(b =>
        {
            b.Property(x => x.DescripcionTiempo).HasColumnType("text");
            b.Property(x => x.DescripcionDisposicion).HasColumnType("text");
            b.Property(x => x.TiempoAg).HasPrecision(5, 2);
            b.Property(x => x.TiempoAc).HasPrecision(5, 2);
            b.Property(x => x.Procedimiento).HasColumnType("text");
            b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.HasOne(x => x.Serie).WithMany(s => s.Subseries).HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("MAESTRA");
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.SerieId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<TipologiaDocumental>(b =>
        {
            b.Property(x => x.FormatosJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
            b.HasOne(x => x.Serie).WithMany().HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Subserie).WithMany().HasForeignKey(x => x.SubserieId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("MAESTRA");
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<CuestionarioCapacitacion>(b =>
        {
            b.Property(x => x.Modulo).HasMaxLength(40).IsRequired();
            b.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
            b.Property(x => x.Descripcion).HasMaxLength(600);
            b.HasIndex(x => new { x.TenantId, x.Modulo });
        });

        modelBuilder.Entity<CuestionarioPregunta>(b =>
        {
            b.Property(x => x.Enunciado).HasColumnType("text").IsRequired();
            b.Property(x => x.OpcionesJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.Retroalimentacion).HasColumnType("text");
            b.HasOne(x => x.Cuestionario).WithMany(c => c.Preguntas).HasForeignKey(x => x.CuestionarioId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.CuestionarioId, x.Orden });
        });

        modelBuilder.Entity<CuestionarioIntento>(b =>
        {
            b.Property(x => x.RespuestasJson).HasColumnType("jsonb").IsRequired();
            b.HasOne(x => x.Cuestionario).WithMany().HasForeignKey(x => x.CuestionarioId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Dependencia).WithMany().HasForeignKey(x => x.DependenciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.DependenciaId });
        });

        modelBuilder.Entity<ProcesoNodo>(b =>
        {
            b.Property(x => x.ElementoBpmnId).HasMaxLength(120).IsRequired();
            b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(250).IsRequired();
            b.Property(x => x.Responsable).HasMaxLength(160);
            b.HasOne(x => x.Proceso).WithMany(p => p.Nodos).HasForeignKey(x => x.ProcesoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ProcesoId, x.ElementoBpmnId }).IsUnique();
        });

        modelBuilder.Entity<ProcesoTransicion>(b =>
        {
            b.Property(x => x.ElementoBpmnId).HasMaxLength(120).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(250);
            b.Property(x => x.Condicion).HasColumnType("text");
            b.HasOne(x => x.Proceso).WithMany(p => p.Transiciones).HasForeignKey(x => x.ProcesoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Origen).WithMany().HasForeignKey(x => x.OrigenId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Destino).WithMany().HasForeignKey(x => x.DestinoId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.ProcesoId, x.ElementoBpmnId }).IsUnique();
        });

        modelBuilder.Entity<NivelTopografico>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            b.Property(x => x.Prefijo).HasMaxLength(10).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Orden }).IsUnique();
        });

        modelBuilder.Entity<ElementoTopografico>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
            b.Property(x => x.CodigoTopografico).HasMaxLength(200).IsRequired();
            b.Property(x => x.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("DISPONIBLE");
            b.HasOne(x => x.Nivel).WithMany().HasForeignKey(x => x.NivelId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Padre).WithMany(p => p.Hijos).HasForeignKey(x => x.PadreId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.CodigoTopografico }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.PadreId });
        });

        modelBuilder.Entity<CargoSerie>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
            b.HasOne(x => x.Serie).WithMany().HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.SerieId, x.Nombre }).IsUnique();
        });

        modelBuilder.Entity<FuncionarioCargo>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.HasOne(x => x.CargoSerie).WithMany(c => c.Funcionarios).HasForeignKey(x => x.CargoSerieId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.CargoSerieId, x.Nombre }).IsUnique();
        });

        modelBuilder.Entity<DirectorioSerie>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.HasOne(x => x.Serie).WithMany().HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Padre).WithMany(p => p.Hijos).HasForeignKey(x => x.PadreId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.SerieId, x.PadreId, x.Nombre }).IsUnique();
        });

        modelBuilder.Entity<Expediente>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
            b.Property(x => x.Descripcion).HasMaxLength(1000);
            b.Property(x => x.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("ABIERTO");
            b.HasOne(x => x.Serie).WithMany().HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Dependencia).WithMany().HasForeignKey(x => x.DependenciaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Estado });
        });

        modelBuilder.Entity<Complemento>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            b.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<CatalogoCaracteristica>(b =>
        {
            b.Property(x => x.EntidadTipo).HasMaxLength(20).IsRequired();
            b.Property(x => x.Clave).HasMaxLength(80).IsRequired();
            b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
            b.Property(x => x.Valor).HasColumnType("text").IsRequired();
            b.HasIndex(x => new { x.EntidadTipo, x.EntidadId, x.Clave }).IsUnique();
        });

        modelBuilder.Entity<TokenDependencia>(b =>
        {
            b.Property(x => x.Token).HasMaxLength(80).IsRequired();
            b.Property(x => x.EmailColaborador).HasMaxLength(160);
            b.HasOne(x => x.Trd).WithMany().HasForeignKey(x => x.TrdId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Dependencia).WithMany().HasForeignKey(x => x.DependenciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.TrdId });
        });

        modelBuilder.Entity<RespuestaTablaDocumental>(b =>
        {
            b.Property(x => x.TiempoAg).HasColumnType("numeric(5,2)");
            b.Property(x => x.TiempoAc).HasColumnType("numeric(5,2)");
            b.Property(x => x.TiempoObserv).HasColumnType("text");
            b.Property(x => x.DispObserv).HasColumnType("text");
            b.Property(x => x.Representativo).HasColumnType("text");
            b.Property(x => x.RelacionSig).HasColumnType("text");
            b.Property(x => x.Extension).HasColumnType("jsonb");
            b.HasOne(x => x.Trd).WithMany().HasForeignKey(x => x.TrdId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Dependencia).WithMany().HasForeignKey(x => x.DependenciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Serie).WithMany().HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Subserie).WithMany().HasForeignKey(x => x.SubserieId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Tipologia).WithMany().HasForeignKey(x => x.TipologiaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.TrdId, x.DependenciaId });
        });

        modelBuilder.Entity<FormatoSerie>(b =>
        {
            b.Property(x => x.Soporte).HasMaxLength(20).IsRequired();
            b.Property(x => x.Formato).HasMaxLength(60).IsRequired();
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.HasOne(x => x.Respuesta).WithMany(r => r.Formatos).HasForeignKey(x => x.RespuestaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.RespuestaId, x.Soporte, x.Formato }).IsUnique();
        });

        modelBuilder.Entity<ColaboradorDependencia>(b =>
        {
            b.Property(x => x.Email).HasMaxLength(160).IsRequired();
            b.Property(x => x.Rol).HasMaxLength(40).IsRequired();
            b.HasOne(x => x.Dependencia).WithMany().HasForeignKey(x => x.DependenciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.DependenciaId, x.Email }).IsUnique();
        });

        modelBuilder.Entity<FormacionDependencia>(b =>
        {
            b.Property(x => x.Modulo).HasMaxLength(40).IsRequired();
            b.HasOne(x => x.Colaborador).WithMany().HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ColaboradorId });
        });

        modelBuilder.Entity<Radicado>(b =>
        {
            b.Property(x => x.Sucursal).HasMaxLength(40).IsRequired();
            b.Property(x => x.Numero).HasMaxLength(60).IsRequired();
            b.Property(x => x.Asunto).HasMaxLength(500).IsRequired();
            b.Property(x => x.Remitente).HasMaxLength(300);
            b.Property(x => x.Estado).HasMaxLength(40).IsRequired();
            b.HasOne(x => x.Tipologia).WithMany().HasForeignKey(x => x.TipologiaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.Sucursal, x.Numero }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Estado, x.FechaRadicacion });
        });

        modelBuilder.Entity<Bodega>(b =>
        {
            b.Property(x => x.Sucursal).HasMaxLength(40).IsRequired();
            b.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.Property(x => x.Direccion).HasMaxLength(300);
            b.HasIndex(x => new { x.TenantId, x.Sucursal, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<Caja>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            b.HasOne(x => x.Bodega).WithMany(x => x.Cajas).HasForeignKey(x => x.BodegaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<Carpeta>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            b.Property(x => x.Titulo).HasMaxLength(300);
            b.HasOne(x => x.Caja).WithMany(x => x.Carpetas).HasForeignKey(x => x.CajaId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Tipologia).WithMany().HasForeignKey(x => x.TipologiaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<ArchivoDigital>(b =>
        {
            b.Property(x => x.Sucursal).HasMaxLength(40).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
            b.Property(x => x.Descripcion).HasMaxLength(1000);
            b.Property(x => x.Bucket).HasMaxLength(100).IsRequired();
            b.Property(x => x.BlobKey).HasMaxLength(400).IsRequired();
            b.Property(x => x.Mime).HasMaxLength(150).IsRequired();
            b.Property(x => x.Sha256).HasMaxLength(64);
            b.Property(x => x.EstadoAprobacion).HasMaxLength(20).IsRequired();
            b.Property(x => x.FaseArchivistica).HasMaxLength(20).IsRequired().HasDefaultValue("GESTION");
            b.Property(x => x.IdentificadorPrincipal).HasMaxLength(120);
            b.Property(x => x.Concepto).HasMaxLength(300);
            b.Property(x => x.RechazoMotivo).HasColumnType("text");
            b.HasOne(x => x.Carpeta).WithMany().HasForeignKey(x => x.CarpetaId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CarpetaArchivo).WithMany().HasForeignKey(x => x.CarpetaArchivoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Tipologia).WithMany().HasForeignKey(x => x.TipologiaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.FechaSubida });
            b.HasIndex(x => new { x.TenantId, x.EstadoAprobacion });
            b.HasIndex(x => new { x.TenantId, x.FlagIdentificado });
            b.HasIndex(x => new { x.TenantId, x.IdentificadorPrincipal });
            b.HasIndex(x => new { x.TenantId, x.CarpetaId });
            b.HasOne(x => x.Expediente).WithMany().HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Dependencia).WithMany().HasForeignKey(x => x.DependenciaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.FaseArchivistica });
            b.HasIndex(x => new { x.TenantId, x.ExpedienteId });
            b.HasIndex(x => new { x.TenantId, x.DependenciaId });
        });

        // ----- 2.D3 Archivo Central: carpetas de clasificacion, tags y aprobacion -----
        modelBuilder.Entity<CarpetaArchivo>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.HasOne(x => x.Padre).WithMany(p => p.Hijos).HasForeignKey(x => x.PadreId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.PadreId, x.Nombre }).IsUnique();
        });

        modelBuilder.Entity<Tag>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
            b.Property(x => x.ColorHex).HasMaxLength(7);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<ArchivoTag>(b =>
        {
            b.HasOne(x => x.Archivo).WithMany().HasForeignKey(x => x.ArchivoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ArchivoId, x.TagId }).IsUnique();
        });

        modelBuilder.Entity<AprobacionDocumento>(b =>
        {
            b.Property(x => x.Decision).HasMaxLength(20).IsRequired();
            b.Property(x => x.Comentario).HasColumnType("text");
            b.HasOne(x => x.Archivo).WithMany().HasForeignKey(x => x.ArchivoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ArchivoId, x.DecididoEn });
        });

        modelBuilder.Entity<ProcesoDefinicion>(b =>
        {
            b.Property(x => x.BpmnXml).HasColumnType("text");
            b.Property(x => x.Sucursal).HasMaxLength(40).IsRequired();
            b.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Sucursal, x.Codigo, x.Version }).IsUnique();
        });

        modelBuilder.Entity<ProcesoActividad>(b =>
        {
            b.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
            b.Property(x => x.Detalle).HasColumnType("text");
            b.HasOne(x => x.Proceso).WithMany(p => p.Actividades).HasForeignKey(x => x.ProcesoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ProcesoId, x.Orden });
        });

        modelBuilder.Entity<ProcesoInstancia>(b =>
        {
            b.Property(x => x.Estado).HasMaxLength(40).IsRequired();
            b.HasOne(x => x.Proceso).WithMany().HasForeignKey(x => x.ProcesoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Radicado).WithMany().HasForeignKey(x => x.RadicadoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.Estado });
        });

        modelBuilder.Entity<Tarea>(b =>
        {
            b.Property(x => x.ActividadNombre).HasMaxLength(300).IsRequired();
            b.Property(x => x.Estado).HasMaxLength(40).IsRequired();
            b.HasOne(x => x.Instancia).WithMany(i => i.Tareas).HasForeignKey(x => x.InstanciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Actividad).WithMany().HasForeignKey(x => x.ActividadId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.AsignadoId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.InstanciaId });
        });

        // ----- 2.D5 Power BI Servicios -----
        modelBuilder.Entity<BiServicio>(b =>
        {
            b.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.SchemaConsulta).HasColumnType("jsonb").IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<BiTokenUso>(b =>
        {
            b.Property(x => x.Token).HasMaxLength(120).IsRequired();
            b.Property(x => x.Parametros).HasColumnType("jsonb").IsRequired();
            b.HasOne(x => x.Servicio).WithMany(s => s.Tokens).HasForeignKey(x => x.ServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ServicioId });
        });

        modelBuilder.Entity<BiLog>(b =>
        {
            b.Property(x => x.Error).HasColumnType("text");
            b.HasOne(x => x.Servicio).WithMany().HasForeignKey(x => x.ServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ServicioId, x.Fecha });
        });
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var applyMethod = typeof(DokTrinoDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                applyMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        // Fail-closed: si no hay tenant activo, TenantId del contexto es null y no devuelve filas.
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }
}
