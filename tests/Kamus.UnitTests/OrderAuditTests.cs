using System.Reflection;
using Kamus.Orders.Application;
using Kamus.Orders.Domain;
using Kamus.Orders.Persistence;
using Kamus.Shared.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Kamus.UnitTests;

/// <summary>Ator no histórico do pedido e política de auditoria do módulo Orders (sem banco).</summary>
public sealed class OrderAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static readonly AuditActor Customer = new(AuditActorKind.Customer, Guid.NewGuid(), "Maria Silva", "maria@kamus.test");

    private static readonly AuditActor Admin = new(AuditActorKind.Admin, Guid.NewGuid(), "Administrador Kamus", "admin@kamus.test");

    private static readonly AuditActor FakePay = AuditActor.System("FakePay");

    private static readonly EntityAuditPolicy Policy = BuildPolicy();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Historico_registra_quem_fez_cada_mudanca()
    {
        var order = NewOrder();
        order.StartPayment(Now, Customer);
        order.MarkPaid(Guid.NewGuid(), Now, FakePay);
        order.Ship(Now, Admin, "BR123456789BR");

        order.History.Select(h => (h.Status, h.ActorKind, h.ActorName)).Should().Equal(
            (OrderStatus.Created, "Customer", "Maria Silva"),
            (OrderStatus.AwaitingPayment, "Customer", "Maria Silva"),
            (OrderStatus.Paid, "System", "FakePay"),
            (OrderStatus.Shipped, "Admin", "Administrador Kamus"));
    }

    [Fact]
    public void Historico_sai_em_ordem_cronologica_mesmo_carregado_fora_de_ordem()
    {
        var order = NewOrder();
        order.StartPayment(Now, Customer);
        order.MarkPaid(Guid.NewGuid(), Now.AddMinutes(1), FakePay);
        order.Ship(Now.AddMinutes(2), Admin, "BR123456789BR");

        // O EF preenche o campo na ordem em que o banco devolve as linhas (a consulta não tem ORDER BY).
        var loaded = (List<OrderStatusChange>)typeof(Order).GetField("_history", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(order)!;
        loaded.Reverse();

        order.History.Select(h => h.Status).Should().Equal(
            OrderStatus.Created, OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.Shipped);
    }

    [Fact]
    public void Transicao_recusada_nao_registra_ator()
    {
        var order = NewOrder();

        order.Ship(Now, Admin, "BR1").IsFailure.Should().BeTrue();

        order.History.Should().ContainSingle().Which.Status.Should().Be(OrderStatus.Created);
    }

    [Fact]
    public void Nome_do_ator_e_limitado_ao_tamanho_da_coluna()
    {
        var change = OrderStatusChange.By(AuditActor.System(new string('x', 400)), OrderStatus.Paid, Now, null);

        change.ActorName.Should().HaveLength(OrderStatusChange.ActorNameMaxLength);
        change.ActorKind.Should().Be("System");
    }

    [Fact]
    public void Cliente_nao_ve_quem_opera_a_loja_e_o_backoffice_ve()
    {
        var order = NewOrder();
        order.StartPayment(Now, Customer);
        order.Cancel("Cancelado pela loja: teste", Now, Admin);

        OrderQueries.ToDetail(order).History.Should().OnlyContain(h => h.ActorKind == null && h.ActorName == null);
        OrderQueries.ToDetail(order, includeActors: true).History[^1]
            .Should().Be(new OrderStatusChangeDto("Cancelled", Now, "Cancelado pela loja: teste", "Admin", "Administrador Kamus"));
    }

    [Fact]
    public void Quem_compra_pela_loja_e_cliente_mesmo_com_papel_admin()
    {
        new FixedActor(Admin).Customer().Should().Be(Admin with { Kind = AuditActorKind.Customer });
        new FixedActor(Customer).Customer().Should().Be(Customer);
        new FixedActor(FakePay).Customer().Should().Be(FakePay, "ator automático não vira cliente");
    }

    [Theory]
    [InlineData(OrderStatus.Created, "Criado")]
    [InlineData(OrderStatus.AwaitingPayment, "Aguardando pagamento")]
    [InlineData(OrderStatus.Paid, "Pago")]
    [InlineData(OrderStatus.Shipped, "Enviado")]
    [InlineData(OrderStatus.Delivered, "Entregue")]
    [InlineData(OrderStatus.Cancelled, "Cancelado")]
    [InlineData(OrderStatus.PaymentFailed, "Pagamento recusado")]
    public void Situacao_aparece_em_portugues(OrderStatus status, string label) =>
        OrdersAudit.StatusLabel(status).Should().Be(label);

    [Fact]
    public async Task Pedido_novo_gera_created_com_a_situacao_inicial()
    {
        using var db = OfflineOrders();
        var order = NewOrder();
        order.StartPayment(Now, Customer);
        var entry = db.Add(order);

        AuditAnalyzer.ResolveAction(entry, Policy).Should().Be(AuditActions.Created);
        (await AuditAnalyzer.ChangesAsync(entry, Policy, Ct)).Should().Equal(new AuditChange("Situação", null, "Aguardando pagamento"));
    }

    [Fact]
    public async Task Despacho_gera_shipped_com_situacao_e_rastreio()
    {
        using var db = OfflineOrders();
        var order = LoadedPaidOrder(db);

        order.Ship(Now, Admin, "BR123456789BR");
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(order);

        AuditAnalyzer.ResolveAction(entry, Policy).Should().Be("shipped");
        (await AuditAnalyzer.ChangesAsync(entry, Policy, Ct)).Should().Equal(
            new AuditChange("Situação", "Pago", "Enviado"),
            new AuditChange("Rastreio", null, "BR123456789BR"));
    }

    [Theory]
    [InlineData("payment_failed", "Pagamento recusado")]
    [InlineData("cancelled", "Cancelado")]
    public async Task Fim_do_pagamento_vira_acao_propria(string action, string status)
    {
        using var db = OfflineOrders();
        var order = NewOrder();
        order.StartPayment(Now, Customer);
        db.Attach(order);

        var result = action == "cancelled"
            ? order.Cancel("Tempo para pagamento esgotado.", Now, AuditActor.System("Expiração da reserva"))
            : order.MarkPaymentFailed(Guid.NewGuid(), "Transação não autorizada", Now, FakePay);
        result.IsSuccess.Should().BeTrue();
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(order);

        AuditAnalyzer.ResolveAction(entry, Policy).Should().Be(action);
        (await AuditAnalyzer.ChangesAsync(entry, Policy, Ct)).Should().Equal(new AuditChange("Situação", "Aguardando pagamento", status));
    }

    [Theory]
    [InlineData(OrderStatus.AwaitingPayment, "payment_started")]
    [InlineData(OrderStatus.Paid, "paid")]
    [InlineData(OrderStatus.Delivered, "delivered")]
    [InlineData(OrderStatus.Created, null)]
    public void Acao_segue_a_situacao_nova(OrderStatus status, string? action) =>
        OrdersAudit.ActionFor(status).Should().Be(action);

    [Fact]
    public void Rotulo_usa_o_numero_gerado_no_insert_e_o_detalhe_e_o_destinatario()
    {
        using var db = OfflineOrders();
        var order = LoadedPaidOrder(db);
        db.Entry(order).Property(o => o.Number).CurrentValue = 10003L;

        Policy.ResolveSubjectAfterSave(order).Should().Be(new AuditSubject("Order", order.Id, "Pedido KM10003"));
        Policy.ResolveDetail(order).Should().Be("Maria Silva");
    }

    private static EntityAuditPolicy BuildPolicy()
    {
        var builder = new AuditPolicyBuilder();
        OrdersAudit.Configure(builder);
        return builder.Build().Find(typeof(Order))!;
    }

    /// <summary>O change tracker funciona sem banco: a conexão nunca é aberta.</summary>
    private static OrdersDbContext OfflineOrders() => new(new DbContextOptionsBuilder<OrdersDbContext>()
        .UseNpgsql("Host=offline.invalid;Database=kamus")
        .Options);

    /// <summary>Pedido pago, como se tivesse vindo do banco (valores originais = atuais).</summary>
    private static Order LoadedPaidOrder(OrdersDbContext db)
    {
        var order = NewOrder();
        order.StartPayment(Now, Customer);
        order.MarkPaid(Guid.NewGuid(), Now, FakePay);
        db.Attach(order);
        return order;
    }

    private static Order NewOrder() => Order.Create(
        Guid.NewGuid(),
        [new OrderItem(Guid.NewGuid(), "KM1-AZUL-M", "Camiseta", "masculino/camisetas/camiseta", "Azul", "M", null, 99.90m, 129.90m, 2)],
        new ShippingAddress("Maria Silva", "01310100", "Av. Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
        new ShippingInfo("Sudeste", 19.90m, 3),
        Now,
        Customer);

    private sealed class FixedActor(AuditActor actor) : ICurrentActor
    {
        public AuditActor Actor => actor;

        public bool IsAuditingSuppressed => false;

        public string? CorrelationId => null;

        public string? IpAddress => null;

        public IDisposable ActAs(AuditActor other) => throw new NotSupportedException();

        public IDisposable SuppressAuditing() => throw new NotSupportedException();
    }
}
