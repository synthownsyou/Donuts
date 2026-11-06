using System;
using System.Text;

// Dependency-free compatibility surface for the existing logging call sites.
namespace Cysharp.Text;

internal sealed class Utf8ValueStringBuilder : IDisposable
{
	private readonly StringBuilder _builder = new();

	public Utf8ValueStringBuilder Append(string value)
	{
		_builder.Append(value);
		return this;
	}

	public Utf8ValueStringBuilder Append(char value)
	{
		_builder.Append(value);
		return this;
	}

	public Utf8ValueStringBuilder AppendLine()
	{
		_builder.AppendLine();
		return this;
	}

	public Utf8ValueStringBuilder AppendLine(string value)
	{
		_builder.AppendLine(value);
		return this;
	}

	public Utf8ValueStringBuilder AppendFormat(string format, params object[] args)
	{
		_builder.AppendFormat(format, args);
		return this;
	}

	public void Clear() => _builder.Clear();

	public override string ToString() => _builder.ToString();

	public void Dispose() => _builder.Clear();
}

internal static class ZString
{
	public static Utf8ValueStringBuilder CreateUtf8StringBuilder() => new();
}
