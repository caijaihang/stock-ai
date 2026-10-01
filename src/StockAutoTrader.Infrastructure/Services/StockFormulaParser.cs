using System.Globalization;
using System.Text.RegularExpressions;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 股票公式解析器（支持通达信/同花顺常用语法子集）
/// 支持函数：MA、REF、CROSS、HHV、LLV、ABS、MAX、MIN
/// 支持变量：C/O/H/L/V（收盘价/开盘价/最高价/最低价/成交量）
/// 支持运算符：+ - * / > < >= <= = != AND OR
/// 示例：
///   CROSS(MA(C,5),MA(C,10))
///   C>REF(C,1) AND C>O
///   HHV(H,20)/LLV(L,20) > 1.1
/// </summary>
public class StockFormulaParser
{
    /// <summary>
    /// 单只股票的 K 线数据点
    /// </summary>
    public class KlinePoint
    {
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Volume { get; set; }
    }

    private static readonly Regex TokenRegex = new(
        @"(MA|REF|CROSS|HHV|LLV|ABS|MAX|MIN)\s*\(|[><=!]=|[<>=]|AND|OR|[+\-*/(),]|\d+\.?\d*|[COHLV]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 编译公式为可执行委托
    /// </summary>
    /// <param name="formula">公式字符串</param>
    /// <returns>评估函数：传入 K 线序列和索引，返回是否满足条件</returns>
    public static Func<List<KlinePoint>, int, bool> Compile(string formula)
    {
        var tokens = Tokenize(formula);
        var pos = 0;

        bool Eval(List<KlinePoint> data, int idx)
        {
            var val = ParseExpression(tokens, ref pos, data, idx);
            return val != 0m;
        }

        return Eval;
    }

    /// <summary>
    /// 评估公式在指定索引处是否成立
    /// </summary>
    public static bool Evaluate(string formula, List<KlinePoint> data, int idx)
    {
        var compiled = Compile(formula);
        return compiled(data, idx);
    }

    /// <summary>
    /// 对一组股票批量筛选，返回满足条件的股票代码
    /// </summary>
    /// <param name="formula">选股公式</param>
    /// <param name="stocks">股票代码 -> K线序列 映射</param>
    /// <returns>满足条件的股票代码列表</returns>
    public static List<string> SelectByFormula(string formula, Dictionary<string, List<KlinePoint>> stocks)
    {
        var compiled = Compile(formula);
        var result = new List<string>();
        foreach (var (code, data) in stocks)
        {
            if (data.Count == 0) continue;
            if (compiled(data, data.Count - 1))
                result.Add(code);
        }
        return result;
    }

    private static List<string> Tokenize(string formula)
    {
        var tokens = new List<string>();
        foreach (Match m in TokenRegex.Matches(formula))
        {
            tokens.Add(m.Value);
        }
        return tokens;
    }

    // 递归下降解析：表达式（OR）
    private static decimal ParseExpression(List<string> tokens, ref int pos, List<KlinePoint> data, int idx)
    {
        var left = ParseAnd(tokens, ref pos, data, idx);
        while (pos < tokens.Count && tokens[pos].Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            var right = ParseAnd(tokens, ref pos, data, idx);
            left = (left != 0m || right != 0m) ? 1m : 0m;
        }
        return left;
    }

    private static decimal ParseAnd(List<string> tokens, ref int pos, List<KlinePoint> data, int idx)
    {
        var left = ParseComparison(tokens, ref pos, data, idx);
        while (pos < tokens.Count && tokens[pos].Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            pos++;
            var right = ParseComparison(tokens, ref pos, data, idx);
            left = (left != 0m && right != 0m) ? 1m : 0m;
        }
        return left;
    }

    private static decimal ParseComparison(List<string> tokens, ref int pos, List<KlinePoint> data, int idx)
    {
        var left = ParseAdditive(tokens, ref pos, data, idx);
        while (pos < tokens.Count)
        {
            var op = tokens[pos];
            if (op is ">" or "<" or ">=" or "<=" or "=" or "!=")
            {
                pos++;
                var right = ParseAdditive(tokens, ref pos, data, idx);
                left = op switch
                {
                    ">" => left > right ? 1m : 0m,
                    "<" => left < right ? 1m : 0m,
                    ">=" => left >= right ? 1m : 0m,
                    "<=" => left <= right ? 1m : 0m,
                    "=" => left == right ? 1m : 0m,
                    "!=" => left != right ? 1m : 0m,
                    _ => 0m
                };
            }
            else break;
        }
        return left;
    }

    private static decimal ParseAdditive(List<string> tokens, ref int pos, List<KlinePoint> data, int idx)
    {
        var left = ParseMultiplicative(tokens, ref pos, data, idx);
        while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
        {
            var op = tokens[pos++];
            var right = ParseMultiplicative(tokens, ref pos, data, idx);
            left = op == "+" ? left + right : left - right;
        }
        return left;
    }

    private static decimal ParseMultiplicative(List<string> tokens, ref int pos, List<KlinePoint> data, int idx)
    {
        var left = ParseUnary(tokens, ref pos, data, idx);
        while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
        {
            var op = tokens[pos++];
            var right = ParseUnary(tokens, ref pos, data, idx);
            left = op == "*" ? left * right : (right != 0m ? left / right : 0m);
        }
        return left;
    }

    private static decimal ParseUnary(List<string> tokens, ref int pos, List<KlinePoint> data, int idx)
    {
        if (pos < tokens.Count && tokens[pos] == "-")
        {
            pos++;
            return -ParsePrimary(tokens, ref pos, data, idx);
        }
        return ParsePrimary(tokens, ref pos, data, idx);
    }

    private static decimal ParsePrimary(List<string> tokens, ref int pos, List<KlinePoint> data, int idx)
    {
        if (pos >= tokens.Count) return 0m;
        var tok = tokens[pos];

        // 数字
        if (decimal.TryParse(tok, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            pos++;
            return num;
        }

        // 变量 C/O/H/L/V
        if (tok.Length == 1 && "COHLV".Contains(tok.ToUpper()))
        {
            pos++;
            if (idx < 0 || idx >= data.Count) return 0m;
            return tok.ToUpper() switch
            {
                "C" => data[idx].Close,
                "O" => data[idx].Open,
                "H" => data[idx].High,
                "L" => data[idx].Low,
                "V" => data[idx].Volume,
                _ => 0m
            };
        }

        // 函数调用
        if (tok.EndsWith("("))
        {
            var funcName = tok[..^1].ToUpper();
            pos++;
            var args = new List<decimal>();
            if (pos < tokens.Count && tokens[pos] != ")")
            {
                args.Add(ParseExpression(tokens, ref pos, data, idx));
                while (pos < tokens.Count && tokens[pos] == ",")
                {
                    pos++;
                    args.Add(ParseExpression(tokens, ref pos, data, idx));
                }
            }
            if (pos < tokens.Count && tokens[pos] == ")") pos++;

            return CallFunction(funcName, args, data, idx);
        }

        // 括号
        if (tok == "(")
        {
            pos++;
            var val = ParseExpression(tokens, ref pos, data, idx);
            if (pos < tokens.Count && tokens[pos] == ")") pos++;
            return val;
        }

        pos++;
        return 0m;
    }

    private static decimal CallFunction(string name, List<decimal> args, List<KlinePoint> data, int idx)
    {
        return name switch
        {
            "MA" => args.Count >= 2 ? CalcMA(data, idx, (int)args[1]) : 0m,
            "REF" => args.Count >= 2 ? GetRef(data, idx, (int)args[1]) : 0m,
            "CROSS" => args.Count >= 2 ? (args[0] > args[1] ? 1m : 0m) : 0m,
            "HHV" => args.Count >= 2 ? CalcHHV(data, idx, (int)args[1]) : 0m,
            "LLV" => args.Count >= 2 ? CalcLLV(data, idx, (int)args[1]) : 0m,
            "ABS" => args.Count >= 1 ? Math.Abs(args[0]) : 0m,
            "MAX" => args.Count >= 2 ? Math.Max(args[0], args[1]) : 0m,
            "MIN" => args.Count >= 2 ? Math.Min(args[0], args[1]) : 0m,
            _ => 0m
        };
    }

    private static decimal CalcMA(List<KlinePoint> data, int idx, int period)
    {
        if (period <= 0 || idx < period - 1 || idx >= data.Count) return 0m;
        decimal sum = 0m;
        for (var i = idx - period + 1; i <= idx; i++)
            sum += data[i].Close;
        return sum / period;
    }

    private static decimal GetRef(List<KlinePoint> data, int idx, int n)
    {
        var target = idx - n;
        return (target >= 0 && target < data.Count) ? data[target].Close : 0m;
    }

    private static decimal CalcHHV(List<KlinePoint> data, int idx, int period)
    {
        if (period <= 0 || idx >= data.Count) return 0m;
        var start = Math.Max(0, idx - period + 1);
        decimal max = decimal.MinValue;
        for (var i = start; i <= idx; i++)
            if (data[i].High > max) max = data[i].High;
        return max == decimal.MinValue ? 0m : max;
    }

    private static decimal CalcLLV(List<KlinePoint> data, int idx, int period)
    {
        if (period <= 0 || idx >= data.Count) return 0m;
        var start = Math.Max(0, idx - period + 1);
        decimal min = decimal.MaxValue;
        for (var i = start; i <= idx; i++)
            if (data[i].Low < min) min = data[i].Low;
        return min == decimal.MaxValue ? 0m : min;
    }
}
