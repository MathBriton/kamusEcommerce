using Kamus.Orders.Domain;

namespace Kamus.UnitTests;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(OrderStatus.Created, OrderStatus.AwaitingPayment, true)]
    [InlineData(OrderStatus.Created, OrderStatus.Paid, false)]
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.PaymentFailed, true)]
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.AwaitingPayment, false)]
    [InlineData(OrderStatus.PaymentFailed, OrderStatus.Paid, false)]
    public void Maquina_de_estados(OrderStatus from, OrderStatus to, bool allowed) =>
        OrderStateMachine.CanTransition(from, to).Should().Be(allowed);

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.PaymentFailed)]
    public void Estados_finais(OrderStatus status) => OrderStateMachine.IsTerminal(status).Should().BeTrue();

    [Fact]
    public void Pedido_calcula_totais_e_registra_historico()
    {
        var order = NewOrder();

        order.Subtotal.Should().Be(2 * 99.90m + 150m);
        order.Total.Should().Be(order.Subtotal + 19.90m);
        order.Status.Should().Be(OrderStatus.Created);

        order.StartPayment(Now).IsSuccess.Should().BeTrue();
        var paymentId = Guid.NewGuid();
        order.MarkPaid(paymentId, Now.AddMinutes(1)).IsSuccess.Should().BeTrue();

        order.PaymentId.Should().Be(paymentId);
        order.History.Select(h => h.Status).Should().Equal(OrderStatus.Created, OrderStatus.AwaitingPayment, OrderStatus.Paid);
    }

    [Fact]
    public void Pagamento_confirmado_duas_vezes_nao_duplica_historico()
    {
        var order = NewOrder();
        order.StartPayment(Now);
        order.MarkPaid(Guid.NewGuid(), Now);

        var second = order.MarkPaid(Guid.NewGuid(), Now);

        second.IsFailure.Should().BeTrue();
        second.Error!.Code.Should().Be("orders.invalid_transition");
        order.History.Count(h => h.Status == OrderStatus.Paid).Should().Be(1);
    }

    [Fact]
    public void Pedido_sem_itens_e_invalido()
    {
        var act = () => Order.Create(Guid.NewGuid(), [], Address, new ShippingInfo("Sudeste", 0, 3), Now);

        act.Should().Throw<ArgumentException>();
    }

    private static readonly ShippingAddress Address = new("Maria", "01310100", "Av. Paulista", "1000", null, "Bela Vista", "São Paulo", "SP");

    private static Order NewOrder() => Order.Create(
        Guid.NewGuid(),
        [
            new OrderItem(Guid.NewGuid(), "KM1-AZUL-M", "Camiseta", "masculino/camisetas/camiseta", "Azul", "M", null, 99.90m, 129.90m, 2),
            new OrderItem(Guid.NewGuid(), "KM2-PRETO-40", "Calça", "masculino/calcas/calca", "Preto", "40", null, 150m, 150m, 1),
        ],
        Address,
        new ShippingInfo("Sudeste", 19.90m, 3),
        Now);
}
