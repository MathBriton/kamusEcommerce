using Kamus.Audit.Persistence;
using Kamus.IntegrationTests.Infrastructure;
using Kamus.Shared.Auditing;
using Kamus.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Audit;

/// <summary>
/// Interceptors de soft delete e auditoria de ponta a ponta, com um DbContext de teste
/// (<see cref="ProbeDbContext"/>) gravando em <c>audit.entries</c> no Postgres real.
/// </summary>
public sealed class AuditInterceptorTests(KamusApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly AuditActor Admin = new(AuditActorKind.Admin, Guid.CreateVersion7(), "Administrador Kamus", "admin@kamus.test");

    private static string NewCode() => $"P-{Guid.NewGuid():N}"[..14];

    private async Task<T> InScopeAsync<T>(Func<ProbeDbContext, ICurrentActor, Task<T>> action, AuditActor? actor = null)
    {
        var host = await ProbeHost.GetAsync(factory);
        await using var scope = host.Services.CreateAsyncScope();
        var current = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
        using (current.ActAs(actor ?? Admin))
        {
            return await action(scope.ServiceProvider.GetRequiredService<ProbeDbContext>(), current);
        }
    }

    private async Task<List<AuditEntry>> EntriesAsync(Guid subjectId)
    {
        var host = await ProbeHost.GetAsync(factory);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await db.Entries.AsNoTracking()
            .Where(e => e.SubjectId == subjectId)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .ToListAsync(Ct);
    }

    private async Task<Guid> CreateProbeAsync(params string[] colors) =>
        await InScopeAsync(async (db, _) =>
        {
            var probe = new Probe("Camisa de Linho", NewCode(), 249.90m);
            foreach (var color in colors)
            {
                probe.AddPart(color);
            }

            db.Probes.Add(probe);
            await db.SaveChangesAsync(Ct);
            return probe.Id;
        });

    [Fact]
    public async Task Criacao_gera_entradas_com_ator_rotulo_gerado_no_insert_e_valores()
    {
        var id = await CreateProbeAsync("Areia", "Preto");

        var entries = await EntriesAsync(id);

        entries.Should().HaveCount(3);
        entries.Select(e => (e.EntityType, e.Action)).Should().Equal(("Probe", "created"), ("ProbePart", "created"), ("ProbePart", "created"));
        entries.Should().AllSatisfy(e =>
        {
            e.Module.Should().Be("probe");
            e.SubjectType.Should().Be("Probe");
            e.ActorKind.Should().Be("Admin");
            e.ActorId.Should().Be(Admin.UserId);
            e.ActorName.Should().Be("Administrador Kamus");
            e.ActorEmail.Should().Be("admin@kamus.test");
        });
        entries.Select(e => e.OccurredAt).Distinct().Should().ContainSingle("tudo veio do mesmo SaveChanges");

        var probe = entries[0];
        probe.SubjectLabel.Should().MatchRegex(@"^Probe #\d{4,}$", "o número só existe depois do insert");
        probe.SubjectLabel.Should().NotBe("Probe #0");
        probe.Changes.Should().Contain("\"field\": \"Preço\"").And.Contain("R$ 249,90");
        entries[1].SubjectLabel.Should().Be("Camisa de Linho");
        entries[1].Detail.Should().Be("Cor Areia");
    }

    [Fact]
    public async Task Edicao_registra_antes_e_depois_e_ignora_campos_fora_da_allowlist()
    {
        var id = await CreateProbeAsync();

        await InScopeAsync(async (db, _) =>
        {
            var probe = await db.Probes.SingleAsync(p => p.Id == id, Ct);
            probe.Name = "Camisa de Linho Areia";
            probe.IsActive = true;
            await db.SaveChangesAsync(Ct);

            probe.Notes = "só anotação"; // fora da allowlist: não gera registro
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        var entries = await EntriesAsync(id);
        entries.Select(e => e.Action).Should().Equal("created", "published");
        var published = entries[1];
        published.Changes.Should().Contain("Camisa de Linho Areia").And.Contain("Rascunho").And.Contain("Publicado");
        published.SubjectLabel.Should().StartWith("Probe #");
    }

    [Fact]
    public async Task Excluir_vai_para_a_lixeira_com_os_filhos_restaurar_traz_de_volta_e_excluir_de_novo_apaga_de_vez()
    {
        var id = await CreateProbeAsync("Areia", "Preto");

        // Excluir: soft delete do pai e dos filhos, no mesmo instante.
        await InScopeAsync(async (db, _) =>
        {
            db.Probes.Remove(await db.Probes.Include(p => p.Parts).SingleAsync(p => p.Id == id, Ct));
            await db.SaveChangesAsync(Ct);
            return 0;
        });

        await InScopeAsync(async (db, _) =>
        {
            (await db.Probes.AnyAsync(p => p.Id == id, Ct)).Should().BeFalse("o filtro global esconde o que está na lixeira");
            var trashed = await db.Probes.IgnoreQueryFilters().Include(p => p.Parts).SingleAsync(p => p.Id == id, Ct);
            trashed.DeletedAt.Should().NotBeNull();
            trashed.DeletedById.Should().Be(Admin.UserId);
            trashed.DeletedByName.Should().Be("Administrador Kamus");
            trashed.Parts.Should().HaveCount(2).And.AllSatisfy(p => p.DeletedAt.Should().Be(trashed.DeletedAt));

            // Restaurar: pai e filhos com o mesmo DeletedAt.
            var deletedAt = trashed.DeletedAt;
            db.Restore(trashed);
            foreach (var part in trashed.Parts.Where(p => p.DeletedAt == deletedAt))
            {
                db.Restore(part);
            }

            await db.SaveChangesAsync(Ct);
            return 0;
        });

        // Excluir duas vezes: a segunda apaga de vez (com os filhos).
        await InScopeAsync(async (db, _) =>
        {
            db.Probes.Remove(await db.Probes.Include(p => p.Parts).SingleAsync(p => p.Id == id, Ct));
            await db.SaveChangesAsync(Ct);

            db.ChangeTracker.Clear();
            db.Probes.Remove(await db.Probes.IgnoreQueryFilters().Include(p => p.Parts).SingleAsync(p => p.Id == id, Ct));
            await db.SaveChangesAsync(Ct);

            (await db.Probes.IgnoreQueryFilters().AnyAsync(p => p.Id == id, Ct)).Should().BeFalse();
            (await db.Parts.IgnoreQueryFilters().AnyAsync(p => p.ProbeId == id, Ct)).Should().BeFalse();
            return 0;
        }, AuditActor.System("Expurgo automático"));

        var entries = await EntriesAsync(id);
        entries.Where(e => e.EntityType == "Probe").Select(e => e.Action)
            .Should().Equal("created", "deleted", "restored", "deleted", "purged");
        entries.Where(e => e.EntityType == "ProbePart").GroupBy(e => e.EntityId).Should().HaveCount(2)
            .And.AllSatisfy(g => g.Select(e => e.Action).Should().Equal("created", "deleted", "restored", "deleted", "purged"));

        var purged = entries.Where(e => e.Action == "purged").ToList();
        purged.Should().AllSatisfy(e =>
        {
            e.ActorKind.Should().Be("System");
            e.ActorId.Should().BeNull();
            e.ActorName.Should().Be("Expurgo automático");
        });
        purged.Single(e => e.EntityType == "Probe").Changes.Should().Contain("\"after\": null");
        purged.Where(e => e.EntityType == "ProbePart").Should().AllSatisfy(e => e.SubjectLabel.Should().Be("Camisa de Linho"));
    }

    [Fact]
    public async Task Rotulo_assincrono_pode_consultar_o_proprio_contexto_durante_o_SaveChanges()
    {
        var id = await CreateProbeAsync("Areia", "Preto");

        // Só a variação é carregada: o rótulo (nome do pai) vem de uma consulta ao mesmo DbContext.
        await InScopeAsync(async (db, _) =>
        {
            db.Parts.Remove(await db.Parts.FirstAsync(p => p.ProbeId == id && p.Color == "Preto", Ct));
            return await db.SaveChangesAsync(Ct);
        });

        var deleted = (await EntriesAsync(id)).Single(e => e.Action == "deleted");
        deleted.EntityType.Should().Be("ProbePart");
        deleted.SubjectLabel.Should().Be("Camisa de Linho");
        deleted.Detail.Should().Be("Cor Preto");
    }

    [Fact]
    public async Task Falha_ao_gravar_a_auditoria_desfaz_a_alteracao()
    {
        var host = await ProbeHost.GetAsync(factory);
        var code = NewCode();

        host.Log.Fail = true;
        try
        {
            var save = () => InScopeAsync(async (db, _) =>
            {
                db.Probes.Add(new Probe("Não pode ficar", code, 10m));
                return await db.SaveChangesAsync(Ct);
            });

            await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("Falha simulada*");
        }
        finally
        {
            host.Log.Fail = false;
        }

        var exists = await InScopeAsync((db, _) => db.Probes.IgnoreQueryFilters().AnyAsync(p => p.Code == code, Ct));
        exists.Should().BeFalse("a alteração e a auditoria estão na mesma transação");
    }

    [Fact]
    public async Task Falha_da_alteracao_nao_grava_auditoria_e_descarta_a_transacao()
    {
        var code = NewCode();
        await InScopeAsync(async (db, _) =>
        {
            db.Probes.Add(new Probe("Original", code, 10m));
            return await db.SaveChangesAsync(Ct);
        });

        var duplicate = new Probe("Duplicado", code, 10m);
        await InScopeAsync(async (db, _) =>
        {
            db.Probes.Add(duplicate);
            var save = () => db.SaveChangesAsync(Ct);
            await save.Should().ThrowAsync<DbUpdateException>();
            db.Database.CurrentTransaction.Should().BeNull("a transação aberta pelo interceptor é desfeita na falha");
            return 0;
        });

        (await EntriesAsync(duplicate.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Conflito_de_concorrencia_e_refeito_em_nova_transacao()
    {
        var id = await CreateProbeAsync();

        await InScopeAsync(async (stale, _) =>
        {
            var old = await stale.Probes.SingleAsync(p => p.Id == id, Ct);

            // Outra requisição altera o mesmo registro (xmin muda).
            await InScopeAsync(async (other, _) =>
            {
                (await other.Probes.SingleAsync(p => p.Id == id, Ct)).Price = 199.90m;
                return await other.SaveChangesAsync(Ct);
            });

            var attempts = 0;
            await ConcurrencyRetry.ExecuteAsync(stale, async () =>
            {
                attempts++;
                stale.Database.CurrentTransaction.Should().BeNull("a tentativa anterior não pode deixar transação aberta");
                var probe = attempts == 1 ? old : await stale.Probes.SingleAsync(p => p.Id == id, Ct);
                probe.Name = "Camisa Renomeada";
                return await stale.SaveChangesAsync(Ct);
            });

            attempts.Should().Be(2);
            stale.Database.CurrentTransaction.Should().BeNull();
            return 0;
        });

        var entries = await EntriesAsync(id);
        entries.Select(e => e.Action).Should().Equal("created", "updated", "updated");
        entries[^1].Changes.Should().Contain("Camisa Renomeada");
    }

    [Fact]
    public async Task Supressao_nao_grava_auditoria_mas_mantem_o_soft_delete()
    {
        var id = await InScopeAsync(async (db, current) =>
        {
            using (current.SuppressAuditing())
            {
                var probe = new Probe("Seed", NewCode(), 10m);
                db.Probes.Add(probe);
                await db.SaveChangesAsync(Ct);

                db.Probes.Remove(probe);
                await db.SaveChangesAsync(Ct);
                return probe.Id;
            }
        });

        (await EntriesAsync(id)).Should().BeEmpty();
        var deletedAt = await InScopeAsync((db, _) => db.Probes.IgnoreQueryFilters().Where(p => p.Id == id).Select(p => p.DeletedAt).SingleAsync(Ct));
        deletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Interceptors_por_escopo_reaproveitam_o_service_provider_interno_do_EF()
    {
        // Cada requisição ganha interceptors próprios (com o ator dela); se isso criasse um service
        // provider interno do EF por requisição, a aplicação degradaria e o EF acabaria lançando erro.
        var first = await InScopeAsync((db, _) => Task.FromResult(db.GetService<IModelSource>()));
        var second = await InScopeAsync((db, _) => Task.FromResult(db.GetService<IModelSource>()));

        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task SaveChanges_sincrono_com_algo_a_auditar_e_recusado()
    {
        await InScopeAsync((db, _) =>
        {
            db.Probes.Add(new Probe("Síncrono", NewCode(), 10m));
            var save = () => db.SaveChanges();
            save.Should().Throw<InvalidOperationException>().WithMessage("*SaveChangesAsync*");
            return Task.FromResult(0);
        });
    }
}
