# OrderGenerator e OrderAccumulator

Aplicação web para enviar ordens de compra e venda por FIX 4.4 e controlar a exposição financeira por ativo.

Para entender a estrutura, o fluxo das mensagens e os casos de validação, consulte o [Guia do projeto](docs/guia-do-projeto.md).

## Tecnologias

- C# 13 e .NET 9 nos backends
- Angular 21 no frontend
- QuickFIX/n 1.14.1 para FIX 4.4
- xUnit 2.9.2 para testes automatizados das regras de ordens e exposição

## Pré-requisitos

- .NET 9 SDK
- Node.js 22.20.0 e npm 10.9.3

O repositório inclui um `.gitignore` para artefatos de build, dependências locais e arquivos de sessão FIX.

## Instalação e testes

Na raiz do repositório, restaure e teste os projetos C#:

```powershell
dotnet restore OrderAccumulator/OrderAccumulator.csproj
dotnet restore OrderGenerator/backend/OrderGenerator.csproj
dotnet test tests/OrderFlow.Tests/OrderFlow.Tests.csproj
```

Instale as dependências do frontend e valide o build:

```powershell
cd OrderGenerator/frontend
npm install --legacy-peer-deps
npm run build
```

## Executar a aplicação

Inicie cada componente em um terminal separado, a partir da raiz do repositório. Inicie primeiro o OrderAccumulator:

```powershell
dotnet run --project OrderAccumulator/OrderAccumulator.csproj
```

Depois inicie o backend do OrderGenerator:

```powershell
dotnet run --project OrderGenerator/backend/OrderGenerator.csproj
```

No terceiro terminal, inicie o frontend:

```powershell
cd OrderGenerator/frontend
npm run start -- --host 127.0.0.1 --port 4200
```

Abra [http://localhost:4200](http://localhost:4200) e envie uma ordem pelo formulário. Por exemplo, PETR4, Compra, quantidade 10 e preço R$ 5,25 deve retornar `New` quando a exposição estiver dentro do limite. O frontend chama o backend por HTTP em `localhost:5000`; o OrderGenerator inicia a sessão FIX 4.4 para o OrderAccumulator em `127.0.0.1:5001`.

Para validar o limite, as rejeições, os cálculos de compra/venda e o efeito por ativo, siga os cenários em [specs/001-order-exposure-control/quickstart.md](specs/001-order-exposure-control/quickstart.md).

## Entrega do desafio

Adicione o link deste repositório à questão na plataforma Coodesh e faça o commit final da solução. Se a questão solicitar apresentação em vídeo, grave e envie pela própria plataforma.

> This is a challenge by [Coodesh](https://coodesh.com/)

## Suporte

Para dúvidas sobre o processo seletivo, entre em contato com um especialista pelo chat da plataforma Coodesh.
