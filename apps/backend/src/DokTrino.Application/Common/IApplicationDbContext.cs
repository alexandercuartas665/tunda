using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Common;

/// <summary>
/// Abstraccion del DbContext para los casos de uso de Application, sin acoplar a la
/// implementacion concreta de Infrastructure. Expone solo los conjuntos que la capa necesita.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<PlatformUser> PlatformUsers { get; }
    DbSet<TenantUser> TenantUsers { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantConfiguration> TenantConfigurations { get; }
    DbSet<TenantEvolutionConfig> TenantEvolutionConfigs { get; }
    DbSet<WhatsAppLine> WhatsAppLines { get; }
    DbSet<PipelineStage> PipelineStages { get; }
    DbSet<PipelineFieldDefinition> PipelineFieldDefinitions { get; }
    DbSet<Lead> Leads { get; }
    DbSet<LeadActivity> LeadActivities { get; }
    DbSet<LeadNote> LeadNotes { get; }
    DbSet<LeadFile> LeadFiles { get; }
    DbSet<FollowUpTask> FollowUpTasks { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<MessageTemplate> MessageTemplates { get; }
    DbSet<QuoteTemplate> QuoteTemplates { get; }
    DbSet<TemplateAsset> TemplateAssets { get; }
    DbSet<AiAgent> AiAgents { get; }
    DbSet<AiAgentResource> AiAgentResources { get; }
    DbSet<AiAgentPrompt> AiAgentPrompts { get; }
    DbSet<AiUsageLog> AiUsageLogs { get; }
    DbSet<AutomationRule> AutomationRules { get; }
    DbSet<FormDefinition> FormDefinitions { get; }
    DbSet<FormDefinitionSnapshot> FormDefinitionSnapshots { get; }
    DbSet<TipoProfesional> TiposProfesional { get; }
    DbSet<SubCategoriaProfesional> SubCategoriasProfesional { get; }
    DbSet<Profesional> Profesionales { get; }
    DbSet<ProfesionalSubCategoria> ProfesionalSubCategorias { get; }
    DbSet<ProfesionalAgencia> ProfesionalAgencias { get; }
    DbSet<Rol> Roles { get; }
    DbSet<RolPermiso> RolPermisos { get; }
    DbSet<Sucursal> Sucursales { get; }
    DbSet<TenantUserSucursal> TenantUserSucursales { get; }
    DbSet<RelacionFormulario> RelacionesFormulario { get; }
    DbSet<TipologiaArchivo> TipologiaArchivos { get; }
    DbSet<Pais> Paises { get; }
    DbSet<Departamento> Departamentos { get; }
    DbSet<Municipio> Municipios { get; }
    DbSet<SerieDocumental> SeriesDocumentales { get; }
    DbSet<SubserieDocumental> SubseriesDocumentales { get; }
    DbSet<TipologiaDocumental> TipologiasDocumentales { get; }
    DbSet<SerieDisposicion> SerieDisposiciones { get; }
    DbSet<Radicado> Radicados { get; }
    DbSet<Bodega> Bodegas { get; }
    DbSet<Caja> Cajas { get; }
    DbSet<Carpeta> Carpetas { get; }
    DbSet<ArchivoDigital> ArchivosDigitales { get; }
    DbSet<ProcesoDefinicion> ProcesosDefinicion { get; }
    DbSet<ProcesoActividad> ProcesoActividades { get; }
    DbSet<ProcesoInstancia> ProcesoInstancias { get; }
    DbSet<Tarea> Tareas { get; }
    DbSet<PowerBiReporte> PowerBiReportes { get; }
    DbSet<SaasPlan> SaasPlans { get; }
    DbSet<SaasPlanLimit> SaasPlanLimits { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<TenantPayment> TenantPayments { get; }
    DbSet<WompiMasterConfig> WompiMasterConfigs { get; }
    DbSet<WompiWebhookEvent> WompiWebhookEvents { get; }
    DbSet<EvolutionMasterConfig> EvolutionMasterConfigs { get; }
    DbSet<AiProviderConfig> AiProviderConfigs { get; }
    DbSet<PlatformBranding> PlatformBrandings { get; }
    DbSet<EmailConfig> EmailConfigs { get; }
    DbSet<GoogleAuthConfig> GoogleAuthConfigs { get; }
    DbSet<TenantApiConfig> TenantApiConfigs { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<SuperAdminAuditLog> SuperAdminAuditLogs { get; }
    DbSet<SqlConsoleLog> SqlConsoleLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
