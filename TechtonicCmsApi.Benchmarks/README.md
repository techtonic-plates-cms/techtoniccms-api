# TechtonicCmsApi.Benchmarks

Este projeto contém os benchmarks BenchmarkDotNet para o backend Techtonic CMS.

## Objetivo

- Medir custos de performance do motor ABAC (autorização baseada em atributos) e de operações com JSONB do PostgreSQL.
- Executar microbenchmarks que simulam caminhos críticos de autorização e filtragem de conteúdo.

## Estrutura

- `Program.cs` — launcher do BenchmarkDotNet usando `BenchmarkSwitcher` e `InProcessEmitToolchain`.
- `Benchmarks/` — classes de benchmark para cenários ABAC e JSONB.
- `Infrastructure/` — suporte de contexto e fábrica de `DbContext` para os benchmarks.

## Requisitos

- .NET 10 SDK
- PostgreSQL em execução com acesso a `database:5432`
- Banco de dados de benchmark configurado via migrations
- O projeto principal `TechtonicCmsApi` deve ser referenciado e construído pelo benchmark

## Como executar

1. No diretório do projeto de benchmarks:

```bash
cd /workspaces/techtoniccms-api/TechtonicCmsApi.Benchmarks
```

2. Restaurar ferramentas e dependências, se necessário:

```bash
dotnet restore
```

3. Construir e executar todos os benchmarks:

```bash
dotnet run -c Release -- --filter '*'
```

4. Executar um benchmark específico:

```bash
dotnet run -c Release -- --filter '*AbacCacheBenchmark*'
```

## Resultados

- Os relatórios do BenchmarkDotNet são gerados em `BenchmarkDotNet.Artifacts/results/`.
- Cada benchmark cria um arquivo HTML e outros artefatos de resultado nesta pasta.

## Observações

- Este projeto assume que a infraestrutura já está disponível e não cria containers ou bancos de dados isolados.
- A configuração do banco utilizada nos benchmarks vem da fábrica de contexto em `Infrastructure/BenchmarkDbContextFactory.cs`.
- Para informações gerais do benchmark e scripts de carga adicionais, veja `benchmarks/README.md` no diretório raiz do repositório.
