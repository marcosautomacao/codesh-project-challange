# Guia do OrderGenerator e OrderAccumulator

Este guia explica como as partes do projeto se comunicam, como iniciar a aplicação e como conferir os resultados de compra, venda, rejeição e limite de exposição.

## Visão geral

O projeto tem três processos executáveis. A aplicação roda localmente, sem Docker.

```text
Navegador (Angular :4200)
        │ HTTP POST /orders
        ▼
OrderGenerator (C# :5000, FIX initiator)
        │ FIX 4.4 / QuickFIX/n (:5001)
        ▼
OrderAccumulator (C# FIX acceptor)
```

### Frontend Angular

- `OrderGenerator/frontend/src/app/app.html` apresenta os campos de ativo, lado, quantidade e preço.
- `OrderGenerator/frontend/src/app/app.ts` valida os valores e envia uma solicitação HTTP para `http://localhost:5000/orders`.
- A resposta `{ "execType": "New" }` ou `{ "execType": "Rejected" }` é exibida no formulário.

### Backend do OrderGenerator

- `OrderGenerator/backend/Orders/OrderRequestValidator.cs` confere ativo, lado, quantidade e preço.
- `OrderGenerator/backend/Orders/OrderEndpointMapping.cs` recebe `POST /orders` e devolve HTTP 400 se os campos forem inválidos.
- `OrderGenerator/backend/Fix/FixOrderGateway.cs` cria um `ClOrdID`, transforma a solicitação em um FIX 4.4 `NewOrderSingle` limitado e envia a ordem ao acumulador.
- `OrderGenerator/backend/Fix/FixOrderSessionApplication.cs` associa o `ExecutionReport` recebido ao mesmo `ClOrdID` e retorna seu `ExecType` ao frontend.

### OrderAccumulator

- `OrderAccumulator/Fix/OrderFixSessionApplication.cs` recebe `NewOrderSingle`, avalia a ordem e responde com `ExecutionReport`.
- `OrderAccumulator/Risk/ExposureBook.cs` mantém uma exposição independente para PETR4, VALE3 e VIIA4 enquanto o processo está em execução.
- Compra adiciona `preço × quantidade`; venda subtrai esse valor.
- Uma aceitação responde `ExecType = New` e atualiza a exposição. Uma rejeição responde `ExecType = Rejected` e não a altera.
- No campo FIX `ExecType`, `0` representa `New` e `8` representa `Rejected`; o backend converte esses valores para a resposta HTTP apresentada no formulário.
- O limite é inclusivo: a exposição pode chegar a R$ 100.000.000 ou -R$ 100.000.000; ultrapassar esses valores causa rejeição.
- A exposição é mantida em memória e volta a zero quando o OrderAccumulator é reiniciado.

As mensagens de aplicação usam os tipos gerados de FIX 4.4 dos pacotes `QuickFIXn.FIX44` e `QuickFIXn.Core`. Os arquivos `quickfix.cfg` usam `UseDataDictionary=N`, então o QuickFIX/n não valida as mensagens contra um arquivo XML de dicionário.

## Preparar e executar

### Pré-requisitos

- .NET 9 SDK
- Node.js 22.20.0 e npm 10.9.3

### Executar os testes automatizados

Na raiz do repositório:

```powershell
dotnet test tests/OrderFlow.Tests/OrderFlow.Tests.csproj
```

Os testes xUnit cobrem a validação da solicitação e as regras centrais de exposição. O frontend é validado pelo build e pelos passos manuais abaixo.

### Iniciar a aplicação

Inicie cada parte em um terminal separado, a partir da raiz do repositório. A ordem importa: o aceitador FIX deve estar pronto antes de iniciar o iniciador.

**Terminal 1 — OrderAccumulator**

```powershell
dotnet run --project OrderAccumulator/OrderAccumulator.csproj
```

**Terminal 2 — OrderGenerator**

```powershell
dotnet run --project OrderGenerator/backend/OrderGenerator.csproj
```

Nos dois consoles, confirme a mensagem `FIX logon: FIX.4.4:...`.

**Terminal 3 — Frontend Angular**

```powershell
cd OrderGenerator/frontend
npm install --legacy-peer-deps
npm run start -- --host 127.0.0.1 --port 4200
```

Abra [http://localhost:4200](http://localhost:4200). Para encerrar cada processo, use `Ctrl+C` no terminal correspondente.

## Casos práticos

Reinicie o OrderAccumulator antes de cada sequência indicada como “estado zerado”. Isso reinicia a exposição em memória.

### Caso 1 — Compra e venda válidas

Com estado zerado para PETR4, envie:

1. **Compra**, quantidade `10`, preço `5.25` → `New`; exposição passa a R$ 52,50.
2. **Venda**, quantidade `4`, preço `5.25` → `New`; exposição passa a R$ 31,50.
3. **Venda**, quantidade `10`, preço `5.25` → `New`; exposição passa a -R$ 21,00.

Esse caso mostra como compra e venda alteram o sinal da exposição líquida.

### Caso 2 — Atingir exatamente o limite

Com estado zerado para PETR4, envie as ordens abaixo na sequência:

| Ordem | Valor da ordem | Exposição resultante | Resultado |
|---|---:|---:|---|
| Compra `99999` × R$ 999,99 | R$ 99.998.000,01 | R$ 99.998.000,01 | `New` |
| Compra `99999` × R$ 0,02 | R$ 1.999,98 | R$ 99.999.999,99 | `New` |
| Compra `1` × R$ 0,01 | R$ 0,01 | R$ 100.000.000,00 | `New` |
| Compra `1` × R$ 0,01 | R$ 0,01 | R$ 100.000.000,01 | `Rejected` |

A última rejeição não altera a exposição. Em seguida, uma compra VALE3 de quantidade `1` a R$ 0,01 deve ser `New`, pois VALE3 tem um limite independente.

### Caso 3 — Venda reduz a exposição; rejeição não é contabilizada

Com estado zerado para VIIA4, envie:

1. Compra `99999` a R$ 999,99 → `New`; exposição R$ 99.998.000,01.
2. Compra `2000` a R$ 1,00 → `Rejected`; a exposição proposta seria R$ 100.000.000,01.
3. Venda `99999` a R$ 0,02 → `New`; exposição cai para R$ 99.996.000,03.
4. Compra `2000` a R$ 1,00 → `New`; exposição passa a R$ 99.998.000,03.

A última compra é aceita porque a venda reduziu a exposição e a compra rejeitada não foi somada.

### Caso 4 — Campos fora dos limites

No formulário, confirme que não é possível enviar um ativo diferente dos três permitidos, um lado diferente de Compra/Venda, quantidade `0` ou `100000`, preço `0`, `1000` ou um valor que não seja múltiplo de R$ 0,01, como `0.015`.

## Comandos de validação

Na raiz do repositório:

```powershell
dotnet build OrderGenerator/backend/OrderGenerator.csproj
dotnet build OrderAccumulator/OrderAccumulator.csproj
dotnet test tests/OrderFlow.Tests/OrderFlow.Tests.csproj
```

Para validar o build do frontend:

```powershell
cd OrderGenerator/frontend
npm run build
```
