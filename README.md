# StockAutoTrader - Windows 股票自动监控与模拟交易软件

基于 [AutoHedge](https://github.com/The-Swarm-Corporation/AutoHedge) 二次开发，针对 A 股市场重新设计的 Windows 桌面端股票自动监控与模拟交易系统。

## 功能特性

- **多股票并发监控**：支持同时监控 50+ 只股票。
- **涨跌幅阈值策略**：涨幅达到阈值自动买入，跌幅达到阈值自动卖出。
- **独立策略配置**：每只股票可单独配置基准价、阈值、数量、冷却时间、最大交易次数、交易时段、T+1 等。
- **模拟交易引擎**：内置模拟撮合，支持手续费、滑点、成交价、成交时间、T+1 开关。
- **行情适配层**：默认模拟行情，预留通达信/同花顺本地导出文件读取适配层。
- **数据持久化**：SQLite + EF Core 保存股票池、策略、持仓、委托、成交、日志。
- **WPF 主界面**：股票列表、持仓、委托、成交、日志、账户、设置等 Tab 页。
- **日志系统**：Serilog 按天滚动，同时写入数据库。
- **单元测试**：xUnit 覆盖策略、行情、交易核心逻辑。

## 技术栈

- C# .NET 8
- WPF + MVVM (CommunityToolkit.Mvvm)
- SQLite + EF Core
- Serilog
- xUnit

## 项目结构

```
StockAutoTrader.sln
├── src/
│   ├── StockAutoTrader.Core/          # 实体、接口、枚举、策略
│   ├── StockAutoTrader.Infrastructure/ # 行情、交易、数据库、日志
│   └── StockAutoTrader.App/           # WPF 主程序
├── tests/
│   └── StockAutoTrader.Tests/         # 单元测试
├── docs/
├── scripts/
└── .github/workflows/                 # CI/CD
```

## 依赖安装

```powershell
# 确保已安装 .NET 8 SDK
winget install Microsoft.DotNet.SDK.8

# 还原 NuGet 包
dotnet restore StockAutoTrader.sln
```

## 编译

```powershell
dotnet build StockAutoTrader.sln -c Release
```

## 运行

```powershell
dotnet run --project src/StockAutoTrader.App/StockAutoTrader.App.csproj
```

## 发布单文件 exe

```powershell
dotnet publish src/StockAutoTrader.App/StockAutoTrader.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

## 配置说明

配置文件位于 `src/StockAutoTrader.App/appsettings.json`，运行时会复制到输出目录。

```json
{
  "MarketDataProvider": "Simulated",
  "TradeExecutor": "Simulated",
  "RefreshIntervalMs": 3000,
  "InitialCapital": 1000000,
  "CommissionRate": 0.0003,
  "Slippage": 0.0,
  "T1Enabled": true,
  "TongDaXinExportDirectory": "C:\\TdxExport",
  "TongHuaShunExportDirectory": "C:\\ThsExport",
  "LogDirectory": "Logs",
  "DatabasePath": "stock_auto_trader.db"
}
```

## 通达信/同花顺行情接入

1. 在交易软件中导出股票行情到 CSV。
2. CSV 格式：`代码,名称,最新价,涨跌幅,昨收,开盘,最高,最低,成交量,时间`。
3. 将文件放到配置指定的导出目录，命名为 `<股票代码>.csv`。
4. 修改 `MarketDataProvider` 为 `TongDaXin` 或 `TongHuaShun`。

> 本项目不实现破解、注入、内存修改、绕过登录或规避风控，仅读取本地导出文件。

## 数据库脚本

首次运行程序会自动创建 SQLite 数据库。手动建表脚本见 `scripts/init_database.sql`。

## 测试

```powershell
dotnet test StockAutoTrader.sln
```

## GitHub Actions

提交到 `main` 分支或推送 tag `v*` 会自动触发构建并发布 Release。

## 免责声明

本软件仅供学习研究使用，不构成任何投资建议。使用本软件进行交易风险自负。
