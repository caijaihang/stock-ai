-- StockAutoTrader SQLite 数据库初始化脚本
-- 首次运行程序会自动执行，也可手动运行此脚本

CREATE TABLE IF NOT EXISTS "StockConfigs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_StockConfigs" PRIMARY KEY AUTOINCREMENT,
    "StockCode" TEXT NOT NULL UNIQUE,
    "StockName" TEXT NOT NULL,
    "Market" TEXT NOT NULL,
    "BenchmarkPriceType" TEXT NOT NULL,
    "CustomBenchmarkPrice" TEXT NOT NULL,
    "BuyThresholdPercent" TEXT NOT NULL,
    "SellThresholdPercent" TEXT NOT NULL,
    "BuyQuantity" INTEGER NOT NULL,
    "SellQuantity" INTEGER NOT NULL,
    "CooldownSeconds" INTEGER NOT NULL,
    "MaxBuyTimesPerDay" INTEGER NOT NULL,
    "MaxSellTimesPerDay" INTEGER NOT NULL,
    "TradingHours" TEXT NOT NULL,
    "T1Enabled" INTEGER NOT NULL,
    "AllowRepeatBuy" INTEGER NOT NULL,
    "AllowPartialSell" INTEGER NOT NULL,
    "OrderType" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "LastBuyTriggerTime" TEXT NOT NULL,
    "LastSellTriggerTime" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Positions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Positions" PRIMARY KEY AUTOINCREMENT,
    "StockCode" TEXT NOT NULL UNIQUE,
    "StockName" TEXT NOT NULL,
    "TotalQuantity" INTEGER NOT NULL,
    "AvailableQuantity" INTEGER NOT NULL,
    "AverageCost" TEXT NOT NULL,
    "CurrentPrice" TEXT NOT NULL,
    "MarketValue" TEXT NOT NULL,
    "UnrealizedPnl" TEXT NOT NULL,
    "UnrealizedPnlPercent" TEXT NOT NULL,
    "LastBuyDate" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Orders" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY AUTOINCREMENT,
    "OrderId" TEXT NOT NULL,
    "StockCode" TEXT NOT NULL,
    "StockName" TEXT NOT NULL,
    "Side" TEXT NOT NULL,
    "OrderType" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Quantity" INTEGER NOT NULL,
    "FilledQuantity" INTEGER NOT NULL,
    "Price" TEXT NOT NULL,
    "FilledPrice" TEXT NOT NULL,
    "Commission" TEXT NOT NULL,
    "StrategyTrigger" TEXT NOT NULL,
    "OrderTime" TEXT NOT NULL,
    "FillTime" TEXT NULL,
    "Remark" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Trades" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Trades" PRIMARY KEY AUTOINCREMENT,
    "TradeId" TEXT NOT NULL,
    "OrderId" TEXT NOT NULL,
    "StockCode" TEXT NOT NULL,
    "StockName" TEXT NOT NULL,
    "Side" TEXT NOT NULL,
    "Quantity" INTEGER NOT NULL,
    "Price" TEXT NOT NULL,
    "Amount" TEXT NOT NULL,
    "Commission" TEXT NOT NULL,
    "TradeTime" TEXT NOT NULL,
    "Remark" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "Accounts" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY AUTOINCREMENT,
    "AccountName" TEXT NOT NULL,
    "InitialCapital" TEXT NOT NULL,
    "AvailableCash" TEXT NOT NULL,
    "TotalMarketValue" TEXT NOT NULL,
    "TotalAssets" TEXT NOT NULL,
    "TotalPnl" TEXT NOT NULL,
    "TotalPnlPercent" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "TradingLogs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TradingLogs" PRIMARY KEY AUTOINCREMENT,
    "Timestamp" TEXT NOT NULL,
    "Level" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "StockCode" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Exception" TEXT NULL
);

CREATE INDEX IF NOT EXISTS "IX_Orders_OrderId" ON "Orders" ("OrderId");
CREATE INDEX IF NOT EXISTS "IX_Orders_StockCode" ON "Orders" ("StockCode");
CREATE INDEX IF NOT EXISTS "IX_Trades_OrderId" ON "Trades" ("OrderId");
CREATE INDEX IF NOT EXISTS "IX_Trades_StockCode" ON "Trades" ("StockCode");
CREATE INDEX IF NOT EXISTS "IX_TradingLogs_Timestamp" ON "TradingLogs" ("Timestamp");
CREATE INDEX IF NOT EXISTS "IX_TradingLogs_StockCode" ON "TradingLogs" ("StockCode");
