using System.Reflection;

namespace Kamus.ArchitectureTests;

/// <summary>
/// Garante a regra de dependência do monólito modular: um módulo só pode
/// referenciar os <c>.Contracts</c> de outro módulo, nunca a implementação.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly string[] Modules = ["Catalog", "Inventory", "Cart", "Orders", "Payments", "Identity", "Reporting"];

    public static TheoryData<string> ModuleNames => new(Modules);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Modulo_nao_referencia_implementacao_de_outro_modulo(string module)
    {
        var assembly = Assembly.Load($"Kamus.{module}");

        var forbidden = Modules
            .Where(m => m != module)
            .Select(m => $"Kamus.{m}")
            .ToHashSet();

        var references = assembly.GetReferencedAssemblies().Select(a => a.Name!);

        references.Should().NotIntersectWith(forbidden,
            $"o módulo {module} deve depender apenas de contratos públicos (Kamus.X.Contracts)");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Contratos_nao_dependem_de_implementacoes(string module)
    {
        var assembly = Assembly.Load($"Kamus.{module}.Contracts");

        var references = assembly.GetReferencedAssemblies().Select(a => a.Name!);

        references.Should().NotContain(r => r.StartsWith("Kamus.", StringComparison.Ordinal) && !r.EndsWith(".Contracts", StringComparison.Ordinal),
            "contratos devem ser apenas DTOs/interfaces sem dependência de implementação");
    }
}
