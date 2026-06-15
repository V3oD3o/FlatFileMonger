# FlatFileMonger

FlatFileMonger is a deterministic CSV and fixed-width flat file engine designed
to handle real-world, inconsistent, and irregular data exports without relying
on schema inference or implicit conversions.

The parser operates as a tokenizing state machine with explicit rules for
delimiters, quoting, whitespace, and newline handling. Error recovery and
record alignment are controlled by configuration.

## Core behavior

- Explicit tokenization (Value, QuotedValue, Comma, NewLine, Comment, Invalid)
- Deterministic parsing with no implicit type conversion
- Configurable delimiter, quote character, whitespace handling, and newline mode
- One-time newline detection in `Auto` mode, then deterministic behavior
- Full CSV-compatible quoted field handling, including embedded CR/LF
- Plain field parsing with optional trailing whitespace trimming
- Hash-prefixed comment lines (`#...`) supported
- Invalid tokens trigger controlled recovery (skip to next newline)

## Record handling

- Optional header row; can be overridden with a predefined schema
- Duplicate column names handled by configurable mode (Ignore, Rename, Disallow)
- Field count autodetected from the first record unless explicitly set
- Sparse records:
  - If enabled: missing fields are padded with null or the configured default
  - If disabled: missing fields raise a CsvFormatException
- Long records:
  - If enabled: extra fields are accepted
  - If disabled: extra fields raise a CsvFormatException
- Optional trailer row detection (TRAILER<n>), with row count validation

## Error handling

- Read() throws CsvFormatException on malformed input
- TryRead() captures the error and continues parsing
- Automatic resynchronization after errors (skip to next newline)
- Invalid tokens never terminate the parser; recovery is explicit and predictable

## Typical usage

```
var options = new CsvFormatOptions()
{
    Encoding = Encoding.UTF8,
    HasHeaderRow = true,
    Delimiter = ',',
    QuoteChar = '"',
    PreserveWhiteSpace = false,
    NewLineMode = NewLineModeEnum.Auto,
};

using var reader = new CsvReader("data.csv", options);

if (reader.ReadHeader())
{
    while (reader.Read())
    {
        Console.WriteLine($"{reader["date"]}: {reader["message"]}");
    }
}
```

## When to use FlatFileMonger

FlatFileMonger is designed for environments where CSV input is:

- inconsistent,
- irregular,
- partially malformed,
- or produced by systems that do not strictly follow CSV conventions.

If a human can still interpret the file, FlatFileMonger will likely parse it
under the configured rules.

## License

Apache License 2.0. See LICENSE for details.
