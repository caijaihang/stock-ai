# StockAutoTrader - Windows / Android 股票自动监控与交易软件

基于 A 股市场的 Windows 桌面端 + Android 移动端股票自动监控与交易系统。
**便携版**：所有数据（数据库、日志、订单、行情文件）全部存到程序解压目录，不写 C 盘。

## 功能特性

- **多股票并发监控**：支持同时监控 50+ 只股票。
- **涨跌幅阈值策略**：涨幅达到阈值自动买入，跌幅达到阈值自动卖出。
- **独立策略配置**：每只股票可单独配置基准价、阈值、数量、冷却时间、最大交易次数、交易时段、T+1 等。
- **两种交易模式**：
  - `Simulated`：内置模拟撮合，支持手续费、滑点、成交价、成交时间、T+1 开关。
  - `Real`：文件桥接真实交易，订单写入 `pending/` 目录，成交回报从 `filled/` 目录读取，可对接通达信/同花顺自动交易脚本。
- **真实行情接入**：
  - `TongDaXin`：直接读取通达信安装目录 `vipdoc/` 下的 `.day` 二进制日数据文件 + 监听 CSV 导出文件。
  - `TongHuaShun`：监听同花顺导出的 CSV/TXT 文件，自动解析推送。
  - `Simulated`：随机游走模拟行情，用于开发测试。
- **数据持久化**：SQLite + EF Core 保存股票池、策略、持仓、委托、成交、日志，全部存到便携目录。
- **WPF 主界面**：股票列表、持仓、委托、成交、日志、账户、设置等 Tab 页。
- **Android 客户端**：.NET MAUI 项目，可监控持仓、账户、启动/停止监控，系统通知推送。
- **日志系统**：Serilog 按天滚动，同时写入数据库，全部存到便携目录。
- **内存约束**：单进程内存占用远低于 8GB，适合便携 U 盘运行。

## 技术栈

- C# .NET 8
- WPF + MVVM (CommunityToolkit.Mvvm)
- .NET MAUI (Android)
- SQLite + EF Core
- Serilog
- xUnit

## 项目结构

```
StockAutoTrader.sln
├── src/
│   ├── StockAutoTrader.Core/            # 实体、接口、枚举、策略、便携路径
│   ├── StockAutoTrader.Infrastructure/  # 行情、交易、数据库、日志
│   ├── StockAutoTrader.App/             # WPF 主程序（Windows）
│   └── StockAutoTrader.Android/         # MAUI Android 客户端
├── tests/
│   └── StockAutoTrader.Tests/           # 单元测试
├── docs/
├── scripts/
└── .github/workflows/                   # CI/CD
```

## 便携版目录结构

解压后目录结构：

```
StockAutoTrader/
├── StockAutoTrader.App.exe      # 可执行文件（Windows）
├── appsettings.json             # 配置文件（与 exe 同级）
├── data/                        # 数据库与订单（自动创建）
│   ├── stock_auto_trader.db
│   ├── market/                  # 行情文件目录（通达信/同花顺导出的 CSV）
│   └── orders/
│       ├── pending/             # 待处理订单（真实交易执行器写入）
│       └── filled/              # 已成交回报（交易脚本写入）
└── logs/                        # 日志文件（自动创建）
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

## 发布单文件 exe（便携版）

```powershell
dotnet publish src/StockAutoTrader.App/StockAutoTrader.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

解压后直接运行 `StockAutoTrader.App.exe`，所有数据自动存到解压目录，不写 C 盘。

## 配置说明

配置文件位于 `src/StockAutoTrader.App/appsettings.json`，运行时复制到输出目录。
路径留空表示使用便携目录（自动），填写绝对路径则使用指定位置。

```json
{
  "MarketDataProvider": "TongDaXin",
  "TradeExecutor": "Real",
  "RefreshIntervalMs": 3000,
  "InitialCapital": 1000000,
  "CommissionRate": 0.0003,
  "Slippage": 0.0,
  "T1Enabled": true,
  "TdxInstallDirectory": "D:\\Tdx",
  "TongDaXinExportDirectory": "",
  "TongHuaShunExportDirectory": "",
  "LogDirectory": "",
  "DatabasePath": "",
  "PendingOrdersDirectory": "",
  "FilledOrdersDirectory": ""
}
```

### 字段说明

| 字段 | 说明 |
|---|---|
| `MarketDataProvider` | `Simulated` / `TongDaXin` / `TongHuaShun` |
| `TradeExecutor` | `Simulated` / `Real` |
| `TdxInstallDirectory` | 通达信安装目录（用于读 `.day` 文件），如 `D:\Tdx` |
| `TongDaXinExportDirectory` | 通达信 CSV 导出目录，留空则用 `data/market` |
| `TongHuaShunExportDirectory` | 同花顺 CSV 导出目录，留空则用 `data/market` |
| `DatabasePath` | 留空则用 `data/stock_auto_trader.db`（便携版） |
| `LogDirectory` | 留空则用 `logs/`（便携版） |
| `PendingOrdersDirectory` | 留空则用 `data/orders/pending/` |
| `FilledOrdersDirectory` | 留空则用 `data/orders/filled/` |

## 通达信真实行情接入

**方式一：读取本地 `.day` 日数据文件（推荐，EOD 级别）**

1. 在 `appsettings.json` 中填写 `TdxInstallDirectory` 为通达信安装目录。
2. 程序自动读取 `vipdoc/sh/lday/sh600000.day`、`vipdoc/sz/lday/sz000001.day` 等文件。
3. 提供收盘价、昨收、开高低、成交量。

**方式二：监听 CSV 导出文件（盘中实时）**

1. 在通达信里导出实时行情 CSV，命名 `<股票代码>.csv`。
2. 放到 `data/market/` 目录（或 `TongDaXinExportDirectory` 指定目录）。
3. 程序用 `FileSystemWatcher` 监听，自动解析并推送行情事件。

CSV 格式（逗号分隔）：`代码,名称,最新价,涨跌幅,昨收,开盘,最高,最低,成交量,时间`
也支持制表符分隔的通达信导出格式。

## 同花顺真实行情接入

1. 在同花顺里导出实时行情 CSV，放到 `data/market/` 目录。
2. 程序自动监听并解析。
3. CSV 格式：`代码,名称,最新价,涨跌幅,昨收,开盘,最高,最低,成交量[,时间]`

## 真实交易对接（文件桥接）

`TradeExecutor` 设为 `Real` 时：

1. 本程序把待处理订单写入 `data/orders/pending/<OrderId>.json`。
2. 用户侧的交易脚本（通达信 Python 量化 / 第三方桥）监控 `pending/` 目录，读取订单并在真实交易软件中下单。
3. 成交后脚本把回报写入 `data/orders/filled/<OrderId>.json`，格式：

```json
{
  "OrderId": "对应 pending 文件里的 OrderId",
  "StockCode": "600000",
  "StockName": "浦发银行",
  "Side": "Buy",
  "Quantity": 100,
  "Price": 9.5,
  "Commission": 2.85,
  "FillTime": "2026-09-19T10:30:00"
}
```

4. 本程序轮询 `filled/` 目录，读取回报并更新数据库（持仓、资金、成交记录）。

> 这是最安全的"真实交易"对接方式，不注入、不破解、不绕过登录，完全通过文件交换完成。

## 通达信 Python 量化对接示例

```python
import json, os, time, glob

PENDING = "data/orders/pending"
FILLED  = "data/orders/filled"

while True:
    for f in glob.glob(os.path.join(PENDING, "*.json")):
        if f.endswith("_cancel.json"):
            order_id = os.path.basename(f).replace("_cancel.json", "")
            cancel_order_in_tdx(order_id)  # 在通达信中撤单
            os.remove(f)
            continue
        with open(f) as fp:
            order = json.load(fp)
        # 在通达信中下单
        filled_price = place_order_in_tdx(
            order["StockCode"], order["Side"],
            order["Quantity"], order["Price"])
        # 写成交回报
        report = {
            "OrderId": order["OrderId"],
            "StockCode": order["StockCode"],
            "StockName": order["StockName"],
            "Side": order["Side"],
            "Quantity": order["Quantity"],
            "Price": filled_price,
            "Commission": order["Quantity"] * filled_price * 0.0003,
            "FillTime": time.strftime("%Y-%m-%dT%H:%M:%S")
        }
        out = os.path.join(FILLED, f"{order['OrderId']}.json")
        with open(out, "w") as fp:
            json.dump(report, fp)
        os.remove(f)
    time.sleep(1)
```

## Android 客户端

Android 端使用 .NET MAUI 构建，复用同一套 Core/Infrastructure。

```powershell
# 需要 .NET 8 SDK + Android SDK
dotnet build src/StockAutoTrader.Android/StockAutoTrader.Android.csproj -c Release
```

Android 端功能：
- 查看持仓、账户资金
- 启动/停止监控
- 系统通知推送（买卖成交提醒）
- 后续可接文件桥（与 Windows 端共享 `data/orders/` 目录）

## 数据库脚本

首次运行程序会自动创建 SQLite 数据库（存到便携目录）。手动建表脚本见 `scripts/init_database.sql`。

## 测试

```powershell
dotnet test StockAutoTrader.sln
```

## GitHub Actions

提交到 `main` 分支或推送 tag `v*` 会自动触发构建并发布 Release。

## 免责声明

本软件仅供学习研究使用，不构成任何投资建议。使用本软件进行交易风险自负。
真实交易模式下，请确保交易脚本符合券商与交易所规定，合规使用。
