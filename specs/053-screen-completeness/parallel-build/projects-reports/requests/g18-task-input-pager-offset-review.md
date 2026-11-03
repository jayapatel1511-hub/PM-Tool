# Root review: extreme page offset during C# freeze

No source edit applied. All C# remains held generation18; root owns execution/repair.

New TaskInputList currently evaluates `ordered.Skip((currentPage - 1) * size)`. Http.Paging accepts positive int page and clamps size to200. With page2147483647/size200, unchecked int multiplication becomes-400, so a syntactically valid read can reach a negative SQL offset. This is a concrete arithmetic boundary visible in the current source, not runtime evidence.

Recommended root-owned bounded repair when safe: compute `(long)(currentPage - 1) * size`; if it is beyond the already-counted total, return the ordinary empty page with unchanged Total/KnownTotal/parameters/asOf; otherwise cast safely to int for Skip. The preceding CountAsync returns int, so any offset below total fits. Add one dedicated max-int page assertion that total remains exact, items empty and no source mutation occurs. Do not clamp to all/first page or change other endpoints/paging semantics. C has not edited held C# or API tests.
