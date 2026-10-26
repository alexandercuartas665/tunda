using DokTrino.Application.Tenancy;

namespace DokTrino.Application.Common;

/// <summary>Genera el PDF del reporte de indicadores de gestion. Implementacion en Infrastructure (QuestPDF).</summary>
public interface IIndicadoresPdfRenderer
{
    byte[] Render(IndicadoresReporteDto data);
}
