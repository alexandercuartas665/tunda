using DokTrino.Domain.Common;

namespace DokTrino.Domain.Entities;

/// <summary>
/// Cargo con permisos sobre una serie documental. Es la configuracion de
/// permiso elevado que despues aplica el modulo Archivo Central: define quien
/// puede subir, editar o eliminar documentos de esa serie y quien la transfiere
/// al archivo central.
/// </summary>
public class CargoSerie : TenantEntity
{
    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public bool PuedeSubir { get; set; } = true;
    public bool PuedeEditar { get; set; }
    public bool PuedeEliminar { get; set; }
    public bool PuedeArchivoCentral { get; set; }

    public ICollection<FuncionarioCargo> Funcionarios { get; set; } = new List<FuncionarioCargo>();
}
