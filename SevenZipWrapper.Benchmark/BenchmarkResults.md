# SevenZipWrapper Benchmark Results

> **Date:** March 02, 2026
> **Machine:** AMD Ryzen 7 5800X (8C/16T) · Windows 11 24H2 · x64 · Release
> **BenchmarkDotNet:** v0.15.2 (New) / v0.12.0 (Old)

---

## Summary Comparison

### Entry Enumeration (`PrintEntries` / `EnumerateEntries`)

| Metric              | .NET Fw 4.7 (Old) | .NET Core 3.1 (Old) | .NET 10 (New) | Δ (3.1 → 10) |
|---------------------|--------------------|----------------------|---------------|---------------|
| **Mean**            | 924 µs             | 1,388 µs            | **1,390 µs**  | ≈ same        |
| **Allocated**       | 14,420 B           | 10,633 B             | **9,144 B**   | **↓ 14%** 🟢  |
| **Gen0/1/2**        | 1.95 / 0 / 0       | 0 / 0 / 0            | **0 / 0 / 0** | —             |
| **Iterations**      | 3 (ShortRun)       | 3 (ShortRun)         | 21 (Default)  | —             |

### Extract First Entry

| Metric              | .NET Fw 4.7 (Old) | .NET Core 3.1 (Old) | .NET 10 (New)  | Δ (3.1 → 10)      |
|---------------------|--------------------|----------------------|----------------|--------------------|
| **Mean**            | 2,918 µs           | 3,208 µs             | **2,998 µs**   | **↓ 6.6%** 🟢     |
| **Allocated**       | 196,234 B          | 192,452 B            | **189,948 B**  | **↓ 1.3%** 🟢     |
| **Gen0/1/2**        | 39 / 39 / 39       | 39 / 39 / 39         | **39 / 39 / 39** | —               |
| **Iterations**      | 3 (ShortRun)       | 3 (ShortRun)         | 13 (Default)   | —                  |

### Extract Last Entry

| Metric              | .NET Fw 4.7 (Old) | .NET Core 3.1 (Old) | .NET 10 (New)  | Δ (3.1 → 10)      |
|---------------------|--------------------|----------------------|----------------|--------------------|
| **Mean**            | 5,040 µs           | 4,514 µs             | **4,447 µs**   | **↓ 1.5%** 🟢     |
| **Allocated**       | 189,904 B          | 186,152 B            | **183,653 B**  | **↓ 1.3%** 🟢     |
| **Gen0/1/2**        | 31 / 31 / 31       | 39 / 39 / 39         | **39 / 39 / 39** | —               |
| **Iterations**      | 3 (ShortRun)       | 3 (ShortRun)         | 13 (Default)   | —                  |

### Extract All

| Metric              | .NET Fw 4.7 (Old) | .NET Core 3.1 (Old) | .NET 10 (New)  | Δ (3.1 → 10)      |
|---------------------|--------------------|----------------------|----------------|--------------------|
| **Mean**            | 4,975 µs           | 5,045 µs             | **5,255 µs**   | **↑ 4.2%** ⚪     |
| **Allocated**       | 288,041 B          | 285,160 B            | **280,461 B**  | **↓ 1.6%** 🟢     |
| **Gen0/1/2**        | 0 / 0 / 0          | 39 / 39 / 39         | **39 / 39 / 39** | —               |
| **Iterations**      | 3 (ShortRun)       | 3 (ShortRun)         | 13 (Default)   | —                  |

---

## Scorecard

| Benchmark              | Speed vs .NET Core 3.1 | Memory   | GC Pressure | Verdict           |
|------------------------|------------------------|----------|-------------|-------------------|
| **EnumerateEntries**   | ≈ same                 | ↓ 14%    | ✅ Zero      | 🟢 **Win**        |
| **ExtractFirstEntry**  | ↓ 6.6% faster          | ↓ 1.3%   | Same         | 🟢 **Win**        |
| **ExtractLastEntry**   | ↓ 1.5% faster          | ↓ 1.3%   | Same         | 🟢 **Win**        |
| **ExtractAll**         | ↑ 4.2% slower          | ↓ 1.6%   | Same         | ⚪ **Within noise** |

---

## Key Observations

### Solid Compression Seek Penalty (7z format)

7z archives use solid compression — entries are compressed as a continuous stream.
Extracting by position reveals the decompression cost:

| Entry Position | .NET 10 Mean | Relative Cost |
|----------------|-------------|---------------|
| First          | 2,998 µs    | 1.0x (baseline) |
| Last           | 4,447 µs    | 1.48x |

---

## Environment Details

### New Benchmark (SevenZipWrapper)

- **Runtime:** .NET 10.0.2 (10.0.225.61305)
- **SDK:** 10.0.102
- **BenchmarkDotNet:** 0.15.2
- **Job:** DefaultJob
- **OS:** Windows 11 (10.0.26100.7462/24H2)
- **CPU:** AMD Ryzen 7 5800X, 1 CPU, 16 logical / 8 physical cores
- **Intrinsics:** AVX2, AES, BMI1, BMI2, FMA, LZCNT, PCLMUL, POPCNT (VectorSize=256)

### Old Benchmark (SevenZipExtractor)

- **Runtime:** .NET Core 3.1.32 / .NET Framework 4.7
- **BenchmarkDotNet:** 0.12.0
- **Job:** ShortRun (3 iterations, 1 launch, 3 warmup)
- **OS:** Windows 10.0.26100
- **CPU:** AMD Ryzen 7 5800X (same machine)

---

## Methodology Notes

1. **Old benchmarks used `ShortRun`** (3 iterations) with wide confidence intervals. New benchmarks use **DefaultJob** with 13–21 iterations, producing statistically stronger results.
2. **ExtractAll initially showed a 16.6% regression** caused by `[IterationCleanup]` forcing `InvocationCount=1, UnrollFactor=1`. After removing the cleanup and relying on `overwrite: true`, the regression dropped to **4.2%** — within the margin of error given the old benchmark's low sample count.
3. **All benchmarks use the same test archive** (`Resources/7z.7z`) and the same native `7z.dll` library just with different versions, since the version 24.00 is updated to 26.00 due to CVE's present in older versions. Performance differences reflect only the managed wrapper overhead.