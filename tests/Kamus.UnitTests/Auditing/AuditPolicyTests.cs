using Kamus.Shared.Auditing;

namespace Kamus.UnitTests.Auditing;

public sealed class AuditPolicyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly EntityAuditPolicy GadgetPolicy = BuildPolicy().Find(typeof(Gadget))!;

    private static AuditPolicy BuildPolicy()
    {
        var builder = new AuditPolicyBuilder()
            .Module("catalog")
            .Entity<Gadget>(e => e
                .Subject("Product", g => g.Id, g => g.Name)
                .Track(g => g.Name, "Nome")
                .Track(g => g.Price, "Preço")
                .Track(g => g.IsActive, "Situação", v => v is true ? "Publicado" : "Rascunho")
                .TrackOnCreate(g => g.Code, "Código")
                .Action(entry => entry.HasChanged(g => g.IsActive) ? (entry.Entity.IsActive ? "published" : "unpublished") : null))
            .Entity<GadgetPart>(e => e.Track(p => p.Color, "Cor"));
        return builder.Build();
    }

    [Fact]
    public async Task Inclusao_gera_created_com_antes_nulo()
    {
        using var db = new OfflineDbContext();
        var entry = db.Add(new Gadget { IsActive = true });

        AuditAnalyzer.ResolveAction(entry, GadgetPolicy).Should().Be(AuditActions.Created);
        var changes = await AuditAnalyzer.ChangesAsync(entry, GadgetPolicy, Ct);

        changes.Should().Equal(
            new AuditChange("Nome", null, "Camisa de Linho"),
            new AuditChange("Preço", null, "R$ 249,90"),
            new AuditChange("Situação", null, "Publicado"),
            new AuditChange("Código", null, "KM001"));
    }

    [Fact]
    public async Task Edicao_registra_so_os_campos_da_allowlist_que_mudaram()
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(new Gadget());
        gadget.Name = "Camisa de Linho Areia";
        gadget.UpdatedAt = DateTimeOffset.UtcNow; // fora da allowlist
        gadget.Code = "KM999";                    // só auditado na criação
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(gadget);

        var action = AuditAnalyzer.ResolveAction(entry, GadgetPolicy)!;
        var changes = await AuditAnalyzer.ChangesAsync(entry, GadgetPolicy, Ct);

        action.Should().Be(AuditActions.Updated);
        changes.Should().Equal(new AuditChange("Nome", "Camisa de Linho", "Camisa de Linho Areia"));
        AuditAnalyzer.ShouldRecord(action, changes).Should().BeTrue();
    }

    [Fact]
    public async Task Edicao_sem_mudanca_na_allowlist_e_descartada()
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(new Gadget());
        gadget.UpdatedAt = DateTimeOffset.UtcNow;
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(gadget);

        var action = AuditAnalyzer.ResolveAction(entry, GadgetPolicy)!;
        var changes = await AuditAnalyzer.ChangesAsync(entry, GadgetPolicy, Ct);

        changes.Should().BeEmpty();
        AuditAnalyzer.ShouldRecord(action, changes).Should().BeFalse();
    }

    [Theory]
    [InlineData(false, true, "published", "Rascunho", "Publicado")]
    [InlineData(true, false, "unpublished", "Publicado", "Rascunho")]
    public async Task Politica_refina_a_acao(bool before, bool after, string expected, string from, string to)
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(new Gadget { IsActive = before });
        gadget.IsActive = after;
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(gadget);

        AuditAnalyzer.ResolveAction(entry, GadgetPolicy).Should().Be(expected);
        (await AuditAnalyzer.ChangesAsync(entry, GadgetPolicy, Ct)).Should().Equal(new AuditChange("Situação", from, to));
    }

    [Fact]
    public void Acao_refinada_e_registrada_mesmo_sem_campo_rastreado() =>
        AuditAnalyzer.ShouldRecord("stock_sold", []).Should().BeTrue();

    [Fact]
    public void Soft_delete_e_restauracao_viram_deleted_e_restored()
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(new Gadget { IsActive = true });
        gadget.MarkDeleted(DateTimeOffset.UtcNow);
        gadget.IsActive = false; // o refinamento não é consultado em exclusões
        db.ChangeTracker.DetectChanges();

        AuditAnalyzer.ResolveAction(db.Entry(gadget), GadgetPolicy).Should().Be(AuditActions.Deleted);

        var trashed = new Gadget();
        trashed.MarkDeleted(DateTimeOffset.UtcNow.AddDays(-1));
        db.Loaded(trashed);
        db.Restore(trashed);
        db.ChangeTracker.DetectChanges();

        AuditAnalyzer.ResolveAction(db.Entry(trashed), GadgetPolicy).Should().Be(AuditActions.Restored);
        trashed.DeletedAt.Should().BeNull();
        trashed.DeletedByName.Should().BeNull();
    }

    [Fact]
    public async Task Exclusao_definitiva_gera_purged_com_depois_nulo()
    {
        using var db = new OfflineDbContext();
        var gadget = db.Loaded(new Gadget());
        db.Remove(gadget);
        var entry = db.Entry(gadget);

        AuditAnalyzer.ResolveAction(entry, GadgetPolicy).Should().Be(AuditActions.Purged);
        (await AuditAnalyzer.ChangesAsync(entry, GadgetPolicy, Ct)).Should().Equal(
            new AuditChange("Nome", "Camisa de Linho", null),
            new AuditChange("Preço", "R$ 249,90", null),
            new AuditChange("Situação", "Rascunho", null));
    }

    [Fact]
    public void Politica_exige_modulo_e_acesso_direto_a_propriedade()
    {
        var semModulo = () => new AuditPolicyBuilder().Entity<Gadget>(e => e.Track(g => g.Name, "Nome")).Build();
        var expressao = () => new AuditPolicyBuilder().Module("catalog").Entity<Gadget>(e => e.Track(g => g.Name.Length, "Tamanho"));
        var duplicada = () => new AuditPolicyBuilder().Module("catalog").Entity<Gadget>(e => e.Track(g => g.Name, "Nome").Track(g => g.Name, "Nome"));

        semModulo.Should().Throw<InvalidOperationException>();
        expressao.Should().Throw<ArgumentException>();
        duplicada.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Entidade_sem_politica_nao_e_auditada() =>
        BuildPolicy().Find(typeof(string)).Should().BeNull();

    public static TheoryData<decimal, string> Money => new()
    {
        { 249.9m, "R$ 249,90" },
        { 1249.5m, "R$ 1.249,50" },
        { 1234567.891m, "R$ 1.234.567,89" },
        { 0m, "R$ 0,00" },
        { -10m, "-R$ 10,00" },
    };

    [Theory]
    [MemberData(nameof(Money))]
    public void Formata_dinheiro_em_reais(decimal value, string expected) =>
        AuditFormat.Value(value).Should().Be(expected);

    [Fact]
    public void Formatacao_padrao()
    {
        AuditFormat.Value(null).Should().BeNull();
        AuditFormat.Value(true).Should().Be("Sim");
        AuditFormat.Value(false).Should().Be("Não");
        AuditFormat.Value(12).Should().Be("12");
        AuditFormat.Value(1.5).Should().Be("1.5");
        AuditFormat.Value("Areia").Should().Be("Areia");
    }

    [Fact]
    public void Ids_do_mesmo_instante_seguem_a_ordem_de_geracao()
    {
        var at = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        var ids = Enumerable.Range(0, 5000).Select(i => AuditIds.Sequential(at, i)).ToList();

        ids.Should().BeInAscendingOrder();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().AllSatisfy(id => id.Version.Should().Be(7));
        AuditIds.Sequential(at.AddMilliseconds(1), 0).CompareTo(ids[^1]).Should().BePositive();
    }
}
