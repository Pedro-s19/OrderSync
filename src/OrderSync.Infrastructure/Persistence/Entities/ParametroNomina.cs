using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class ParametroNomina
{
    public string Clave { get; set; } = null!;

    public decimal Valor { get; set; }

    public string? Descripcion { get; set; }

    public int Vigencia { get; set; }
}
