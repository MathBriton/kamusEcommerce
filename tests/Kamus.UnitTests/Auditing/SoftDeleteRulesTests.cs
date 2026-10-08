using Kamus.Shared.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.UnitTests.Auditing;

public sealed class SoftDeleteRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly AuditActor Admin = new(AuditActorKind.Admin, Guid.CreateVersion7(), "Administrador Kamus", "admin@kamus.dev");

    private static Gadget WithParts(int count)
    {
        var gadget = new Gadget();
        for (var i = 0; i < count; i++)
        {
            gadget.Parts.Add(new GadgetPart { GadgetId = gadget.Id, Color = $"Cor {i}" });
        }

        return gadget;
    }

    [Fact]
    public void Remover_item_ativo_vai_para_a_lixeira_com_os_filhos_no_mesmo_instante()
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(WithParts(2));
        db.Remove(gadget); // o cascade do EF marca as peças como Deleted

        SoftDeleteRules.Apply(db, Admin, Now);

        db.Entry(gadget).State.Should().Be(EntityState.Modified);
        gadget.DeletedAt.Should().Be(Now);
        gadget.DeletedById.Should().Be(Admin.UserId);
        gadget.DeletedByName.Should().Be("Administrador Kamus");
        gadget.Parts.Should().AllSatisfy(p =>
        {
            db.Entry(p).State.Should().Be(EntityState.Modified);
            p.DeletedAt.Should().Be(Now);
        });
    }

    [Fact]
    public void Soft_delete_atualiza_so_as_colunas_da_exclusao()
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(new Gadget());
        db.Remove(gadget);

        SoftDeleteRules.Apply(db, Admin, Now);

        var modified = db.Entry(gadget).Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name);
        modified.Should().BeEquivalentTo(nameof(Gadget.DeletedAt), nameof(Gadget.DeletedById), nameof(Gadget.DeletedByName));
    }

    [Fact]
    public void Remover_o_que_ja_esta_na_lixeira_apaga_de_vez()
    {
        using var db = new OfflineDbContext();
        var gadget = WithParts(1);
        gadget.MarkDeleted(Now.AddDays(-3));
        gadget.Parts[0].MarkDeleted(Now.AddDays(-3));
        db.Loaded(gadget);
        db.Remove(gadget);

        SoftDeleteRules.Apply(db, Admin, Now);

        db.Entry(gadget).State.Should().Be(EntityState.Deleted);
        db.Entry(gadget.Parts[0]).State.Should().Be(EntityState.Deleted);
        gadget.DeletedAt.Should().Be(Now.AddDays(-3));
    }

    [Fact]
    public void Filho_ja_na_lixeira_nao_e_expurgado_pelo_cascade_de_um_soft_delete()
    {
        using var db = new OfflineDbContext();
        var gadget = WithParts(2);
        gadget.Parts[1].MarkDeleted(Now.AddDays(-5)); // excluído antes, sozinho
        db.Loaded(gadget);
        db.Remove(gadget);

        SoftDeleteRules.Apply(db, Admin, Now);

        db.Entry(gadget.Parts[0]).State.Should().Be(EntityState.Modified);
        gadget.Parts[0].DeletedAt.Should().Be(Now);
        db.Entry(gadget.Parts[1]).State.Should().Be(EntityState.Unchanged);
        gadget.Parts[1].DeletedAt.Should().Be(Now.AddDays(-5), "ele continua com a data da exclusão dele, e não volta junto");
    }

    [Fact]
    public void Filho_ativo_de_item_expurgado_tambem_e_expurgado()
    {
        using var db = new OfflineDbContext();
        var gadget = WithParts(1);
        gadget.MarkDeleted(Now.AddDays(-3));
        db.Loaded(gadget);
        db.Remove(gadget);

        SoftDeleteRules.Apply(db, Admin, Now);

        db.Entry(gadget.Parts[0]).State.Should().Be(EntityState.Deleted);
    }

    [Fact]
    public void Excluir_so_o_filho_nao_afeta_o_pai()
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(WithParts(2));
        db.Remove(gadget.Parts[0]);

        SoftDeleteRules.Apply(db, Admin, Now);

        db.Entry(gadget).State.Should().Be(EntityState.Unchanged);
        db.Entry(gadget.Parts[0]).State.Should().Be(EntityState.Modified);
        gadget.Parts[0].DeletedAt.Should().Be(Now);
        gadget.Parts[1].DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Nome_do_ator_e_cortado_no_tamanho_da_coluna()
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(new Gadget());
        db.Remove(gadget);

        SoftDeleteRules.Apply(db, AuditActor.System(new string('x', 200)), Now);

        gadget.DeletedByName.Should().HaveLength(SoftDelete.NameMaxLength);
        gadget.DeletedById.Should().BeNull();
    }
}
