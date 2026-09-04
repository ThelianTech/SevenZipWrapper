using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters.Json;

using SevenZipWrapper;

[JsonExporterAttribute.Full]
[SupportedOSPlatform("windows")]
[MemoryDiagnoser]
public class Benchmarks {
	private string _outputDir;
	private string _singleFileDir;

	[Params("7z.7z", "LT_Nemesis.7z")]
	public string ArchiveFileName { get; set; }

	private string ArchivePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", ArchiveFileName);

	[GlobalSetup]
	public void Setup() {
		_outputDir = Path.Combine(Path.GetTempPath(), "SevenZipBenchmark");
		_singleFileDir = Path.Combine(Path.GetTempPath(), "SevenZipBenchmarkSingle");
		Directory.CreateDirectory(_singleFileDir);
		Directory.CreateDirectory(_outputDir);
	}

	[GlobalCleanup]
	public void Cleanup() {
		if (Directory.Exists(_singleFileDir)) {
			Directory.Delete(_singleFileDir, recursive: true);
		}

		if (Directory.Exists(_outputDir)) {
			Directory.Delete(_outputDir, recursive: true);
		}
	}

	// -----------------------------------------
	//  Existing Benchmarks (baseline)
	// -----------------------------------------

	[Benchmark]
	public int EnumerateEntries() {
		using var archive = new ArchiveFile(ArchivePath);
		return archive.Entries.Count;
	}

	[Benchmark]
	public void ExtractFirstEntry() {
		using var archive = new ArchiveFile(ArchivePath);
		ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);
		entry.Extract(Path.Combine(_singleFileDir, Path.GetFileName(entry.FileName!)));
	}

	[Benchmark]
	public void ExtractLastEntry() {
		using var archive = new ArchiveFile(ArchivePath);
		ArchiveEntry entry = archive.Entries.Last(e => !e.IsFolder);
		entry.Extract(Path.Combine(_singleFileDir, Path.GetFileName(entry.FileName!)));
	}

	[Benchmark]
	public void ExtractAll() {
		using var archive = new ArchiveFile(ArchivePath);
		archive.Extract(_outputDir, overwrite: true);
	}

	// -----------------------------------------
	//  New Benchmarks — Progress & Cancellation
	// -----------------------------------------

	/// <summary>
	/// ExtractAll with an active progress callback.
	/// Measures the per-file callback overhead added by SetOperationResult.
	/// </summary>
	[Benchmark]
	public void ExtractAllWithProgress() {
		int lastCount = 0;

		using var archive = new ArchiveFile(ArchivePath);
		archive.Extract(
			_outputDir,
			overwrite: true,
			onFileExtracted: count => lastCount = count,
			cancellationToken: CancellationToken.None);
	}

	/// <summary>
	/// ExtractAll with CancellationToken.None (not cancelled).
	/// Measures the per-file IsCancellationRequested check overhead.
	/// </summary>
	[Benchmark]
	public void ExtractAllWithToken() {
		using var archive = new ArchiveFile(ArchivePath);
		archive.Extract(
			_outputDir,
			overwrite: true,
			onFileExtracted: null,
			cancellationToken: CancellationToken.None);
	}

	/// <summary>
	/// ExtractAll with both progress callback and CancellationToken active.
	/// Measures the combined overhead of both features.
	/// </summary>
	[Benchmark]
	public void ExtractAllWithProgressAndToken() {
		int lastCount = 0;

		using var cts = new CancellationTokenSource();
		using var archive = new ArchiveFile(ArchivePath);
		archive.Extract(
			_outputDir,
			overwrite: true,
			onFileExtracted: count => lastCount = count,
			cancellationToken: cts.Token);
	}

	/// <summary>
	/// Extract to MemoryStream — no disk I/O, pure decompression + managed overhead.
	/// </summary>
	[Benchmark]
	public void ExtractToStream() {
		using var archive = new ArchiveFile(ArchivePath);
		ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);

		using var ms = new MemoryStream();
		entry.Extract(ms);
	}

	/// <summary>
	/// Selective extraction via Func callback — extracts only non-folder entries.
	/// Measures the getOutputPath delegate invocation overhead per entry.
	/// </summary>
	[Benchmark]
	public void ExtractSelectiveWithProgress() {
		int lastCount = 0;

		using var archive = new ArchiveFile(ArchivePath);
		archive.Extract(
			getOutputPath: entry =>
				entry.IsFolder
					? null
					: Path.Combine(_outputDir, entry.FileName ?? string.Empty),
			onFileExtracted: count => lastCount = count,
			cancellationToken: CancellationToken.None);
	}
}