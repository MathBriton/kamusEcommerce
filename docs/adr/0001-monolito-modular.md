# 0001 — Monólito modular em vez de microsserviços

- **Status:** aceita
- **Data:** 2026-10-06
- **Release:** R0

## Contexto

O Kamus é um e-commerce de moda feito para estudar arquitetura. O domínio tem contextos bem
distintos (catálogo, estoque, carrinho, pedidos, pagamentos e identidade), mas o time é de uma pessoa,
o tráfego é zero e o maior risco é errar as fronteiras entre os contextos. Microsserviços trariam desde
o primeiro dia rede, deploy independente, consistência eventual e observabilidade distribuída, tudo
antes de existir um motivo concreto.

## Decisão

Um único processo ASP.NET Core (`apps/api`) hospeda todos os módulos. Cada módulo:

- vive em `src/modules/<Modulo>/` e tem dois projetos: `Kamus.<Modulo>` (implementação) e
  `Kamus.<Modulo>.Contracts` (interfaces e DTOs públicos);
- implementa `IModule` para registrar serviços e mapear endpoints;
- tem schema próprio no Postgres e nunca lê nem escreve tabelas de outro módulo;
- conversa com outros módulos **apenas** pelos contratos (`*.Contracts`), in-process no MVP e por
  eventos a partir da R3.

A regra de dependência é verificada por testes de arquitetura (`tests/Kamus.ArchitectureTests`),
que quebram o build se um módulo referenciar a implementação de outro.

## Alternativas consideradas

- **Microsserviços desde o início** — custo operacional alto sem benefício nesse estágio; fronteiras
  erradas ficam caras de corrigir quando já existe rede no meio.
- **Monólito em camadas sem módulos** — mais simples no início, mas as fronteiras de domínio somem e
  a extração futura fica inviável.

## Consequências

- Um deploy, um banco, transações locais quando necessário; depuração simples.
- As fronteiras são explícitas e testadas, então extrair um módulo vira um trabalho mecânico
  (trocar a chamada in-process por HTTP/mensageria).
- É preciso disciplina: um contrato mal desenhado vaza detalhes de implementação para outros módulos.
- Escala apenas horizontalmente como um todo; aceitável até haver dados de carga (R6).
