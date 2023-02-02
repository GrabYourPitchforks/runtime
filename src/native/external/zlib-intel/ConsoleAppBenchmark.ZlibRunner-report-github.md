``` ini

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.22621.1105), VM=Hyper-V
AMD Ryzen 9 3950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK=7.0.200-preview.22628.1
  [Host]   : .NET 7.0.0 (7.0.22.51805), X64 RyuJIT AVX2
  ShortRun : .NET 7.0.3 (42.42.42.42424), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
|     Method | Toolchain |   level |                   file |       Mean |        Error |    StdDev | Ratio | RatioSD |
|----------- |---------- |-------- |----------------------- |-----------:|-------------:|----------:|------:|--------:|
|   **Compress** |   **compare** | **Optimal** |  **C:\\pe(...)t.pdf [93]** | **3,729.7 μs** |    **715.06 μs** |  **39.19 μs** |  **1.03** |    **0.03** |
|   Compress |      main | Optimal |  C:\\pe(...)t.pdf [93] | 3,606.6 μs |  1,178.33 μs |  64.59 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
| Decompress |   compare | Optimal |  C:\\pe(...)t.pdf [93] |   404.5 μs |     41.28 μs |   2.26 μs |  0.95 |    0.03 |
| Decompress |      main | Optimal |  C:\\pe(...)t.pdf [93] |   427.2 μs |    264.41 μs |  14.49 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
|   **Compress** |   **compare** | **Optimal** |  **C:\\pe(...)9.txt [88]** | **5,210.0 μs** |  **1,589.48 μs** |  **87.12 μs** |  **0.96** |    **0.11** |
|   Compress |      main | Optimal |  C:\\pe(...)9.txt [88] | 5,504.3 μs | 12,272.10 μs | 672.68 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
| Decompress |   compare | Optimal |  C:\\pe(...)9.txt [88] |   521.7 μs |    280.13 μs |  15.35 μs |  1.01 |    0.03 |
| Decompress |      main | Optimal |  C:\\pe(...)9.txt [88] |   517.0 μs |    201.16 μs |  11.03 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
|   **Compress** |   **compare** | **Optimal** | **C:\\pe(...)a\\sum [80]** |   **777.8 μs** |    **466.43 μs** |  **25.57 μs** |  **1.01** |    **0.02** |
|   Compress |      main | Optimal | C:\\pe(...)a\\sum [80] |   772.3 μs |    413.65 μs |  22.67 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
| Decompress |   compare | Optimal | C:\\pe(...)a\\sum [80] |   157.2 μs |     98.73 μs |   5.41 μs |  1.00 |    0.03 |
| Decompress |      main | Optimal | C:\\pe(...)a\\sum [80] |   157.4 μs |     66.97 μs |   3.67 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
|   **Compress** |   **compare** | **Fastest** |  **C:\\pe(...)t.pdf [93]** | **2,658.7 μs** |  **1,499.52 μs** |  **82.19 μs** |  **1.03** |    **0.04** |
|   Compress |      main | Fastest |  C:\\pe(...)t.pdf [93] | 2,579.9 μs |  2,456.89 μs | 134.67 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
| Decompress |   compare | Fastest |  C:\\pe(...)t.pdf [93] |   382.7 μs |    117.18 μs |   6.42 μs |  0.96 |    0.04 |
| Decompress |      main | Fastest |  C:\\pe(...)t.pdf [93] |   398.6 μs |    173.96 μs |   9.54 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
|   **Compress** |   **compare** | **Fastest** |  **C:\\pe(...)9.txt [88]** | **1,697.4 μs** |    **420.08 μs** |  **23.03 μs** |  **1.00** |    **0.01** |
|   Compress |      main | Fastest |  C:\\pe(...)9.txt [88] | 1,701.4 μs |    215.07 μs |  11.79 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
| Decompress |   compare | Fastest |  C:\\pe(...)9.txt [88] |   591.7 μs |    346.91 μs |  19.02 μs |  1.04 |    0.04 |
| Decompress |      main | Fastest |  C:\\pe(...)9.txt [88] |   568.1 μs |    233.40 μs |  12.79 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
|   **Compress** |   **compare** | **Fastest** | **C:\\pe(...)a\\sum [80]** |   **347.1 μs** |    **166.74 μs** |   **9.14 μs** |  **0.98** |    **0.08** |
|   Compress |      main | Fastest | C:\\pe(...)a\\sum [80] |   355.5 μs |    457.27 μs |  25.06 μs |  1.00 |    0.00 |
|            |           |         |                        |            |              |           |       |         |
| Decompress |   compare | Fastest | C:\\pe(...)a\\sum [80] |   151.5 μs |    133.82 μs |   7.34 μs |  0.97 |    0.05 |
| Decompress |      main | Fastest | C:\\pe(...)a\\sum [80] |   155.9 μs |    121.91 μs |   6.68 μs |  1.00 |    0.00 |
