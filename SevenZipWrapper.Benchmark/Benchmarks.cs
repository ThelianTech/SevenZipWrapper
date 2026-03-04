using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters.Json;

using SevenZipWrapper;

[JsonExporterAttribute.Full]
[SupportedOSPlatform("windows")]
[MemoryDiagnoser]
public class Benchmarks {
	private string _archivePath;
	private string _outputDir;
	private string _singleFileDir;

	[GlobalSetup]
	public void Setup() {
		_archivePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "7z.7z");
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

	[Benchmark]
	public int EnumerateEntries() {
		using var archive = new ArchiveFile(_archivePath);
		return archive.Entries.Count;
	}

	[Benchmark]
	public void ExtractFirstEntry() {
		using var archive = new ArchiveFile(_archivePath);
		ArchiveEntry entry = archive.Entries.First(e => !e.IsFolder);
		entry.Extract(Path.Combine(_singleFileDir, Path.GetFileName(entry.FileName!)));
	}

	[Benchmark]
	public void ExtractLastEntry() {
		using var archive = new ArchiveFile(_archivePath);
		ArchiveEntry entry = archive.Entries.Last(e => !e.IsFolder);
		entry.Extract(Path.Combine(_singleFileDir, Path.GetFileName(entry.FileName!)));
	}

	[Benchmark]
	public void ExtractAll() {
		using var archive = new ArchiveFile(_archivePath);
		archive.Extract(_outputDir, overwrite: true);
	}
}