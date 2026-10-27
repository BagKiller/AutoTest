# 工作项 #0 验证报告

- 结论：已验证通过（PASS / OK）
- 说明：OK: 27/27 断言通过
- runId：`20261023T080431Z_wi0_63e3555`
- 用例：commit-8d088291-logic
- 断言：27 过 27（失败 0，跳过 0）
- 被测：commit `10c33df5`，分支 HEAD，未重编（builtFromCurrentSource=false，结论仅对旧二进制有效），工作区有未提交改动
- 起止：2026-10-23T16:04:31+08:00 → 2026-10-23T16:04:34+08:00（3.5 秒）

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.VertexNearEdge_InsufficientSideSupport_ReturnsNaN | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.VertexNearEdge_InsufficientSideSupport_ReturnsNaN | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.ExtractNotchWindowTests.WindowSlice_CopiesPointsAndMask_MapsStartIndex | commit8d088291.Vega.BL.WaferAlignment.Tests.ExtractNotchWindowTests.WindowSlice_CopiesPointsAndMask_MapsStartIndex | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.MinimumCandidateInvalid_AllCandidatesRejected_ReturnsMinusOne | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.MinimumCandidateInvalid_AllCandidatesRejected_ReturnsMinusOne | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.ValidCrossing_AtLastColumn_ReturnsThetaNearTwoPi | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.ValidCrossing_AtLastColumn_ReturnsThetaNearTwoPi | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.ExtractNotchWindowTests.MismatchedMaskLength_ReturnsNull | commit8d088291.Vega.BL.WaferAlignment.Tests.ExtractNotchWindowTests.MismatchedMaskLength_ReturnsNull | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindNotchEdgesForIndexesTests.Masked_MixedColumns_WriteInterpolatedRadiusAndMask | commit8d088291.Vega.BL.WaferAlignment.Tests.FindNotchEdgesForIndexesTests.Masked_MixedColumns_WriteInterpolatedRadiusAndMask | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.GapColumn_SkipsUnbracketedPairs_AndFindsInnerCrossing | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.GapColumn_SkipsUnbracketedPairs_AndFindsInnerCrossing | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.MismatchedMaskLength_ReturnsNaN | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.MismatchedMaskLength_ReturnsNaN | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.AllInvalid_ReturnsMinusOneWithWarning | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.AllInvalid_ReturnsMinusOneWithWarning | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.ValidCrossing_AtOffsetColumn_IncludesColumnFraction | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.ValidCrossing_AtOffsetColumn_IncludesColumnFraction | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.AllValid_SelectsMinimumRadiusCandidate | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.AllValid_SelectsMinimumRadiusCandidate | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.EntirelyAboveThreshold_MarksInvalid | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.EntirelyAboveThreshold_MarksInvalid | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindNotchEdgesForIndexesTests.Unmasked_KeepsFirstCrossingGuessBehavior | commit8d088291.Vega.BL.WaferAlignment.Tests.FindNotchEdgesForIndexesTests.Unmasked_KeepsFirstCrossingGuessBehavior | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.SingleCrossing_InterpolatesRealFraction | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.SingleCrossing_InterpolatesRealFraction | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.TooFewValidPoints_ReturnsNaN | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.TooFewValidPoints_ReturnsNaN | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.AllInvalid_ReturnsNaN | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.AllInvalid_ReturnsNaN | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.VertexOutOfRange_ReturnsNaN | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.VertexOutOfRange_ReturnsNaN | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.NoBracketedCrossing_MarksInvalid_KeepsDefaultRadius | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.NoBracketedCrossing_MarksInvalid_KeepsDefaultRadius | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindNotchEdgesForIndexesTests.MismatchedMaskLength_ThrowsArgumentException | commit8d088291.Vega.BL.WaferAlignment.Tests.FindNotchEdgesForIndexesTests.MismatchedMaskLength_ThrowsArgumentException | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.EqualIntensities_NeverInterpolated_MarksInvalid | commit8d088291.Vega.BL.WaferAlignment.Tests.FindBracketedNotchEdgeInColumnTests.EqualIntensities_NeverInterpolated_MarksInvalid | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.AllValidParabola_ReturnsVertex | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchFineSearchValidTests.AllValidParabola_ReturnsVertex | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.MismatchedMaskLength_ReturnsMinusOneWithWarning | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.MismatchedMaskLength_ReturnsMinusOneWithWarning | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.FindNotchEdgesForIndexesTests.Masked_OutOfBoundsIndex_KeepsDefaultRadiusAndInvalid | commit8d088291.Vega.BL.WaferAlignment.Tests.FindNotchEdgesForIndexesTests.Masked_OutOfBoundsIndex_KeepsDefaultRadiusAndInvalid | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.PartialValid_MeanRadiusUsesValidPointsOnly | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.PartialValid_MeanRadiusUsesValidPointsOnly | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.EmptyPoints_Unmasked_ReturnsMinusOneSilently | commit8d088291.Vega.BL.WaferAlignment.Tests.NotchCoarseSearchCoreTests.EmptyPoints_Unmasked_ReturnsMinusOneSilently | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.ExtractNotchWindowTests.NullMask_ReturnsNull | commit8d088291.Vega.BL.WaferAlignment.Tests.ExtractNotchWindowTests.NullMask_ReturnsNull | specified behavior | matched |
| PASS | commit8d088291.Vega.BL.WaferAlignment.Tests.ExtractNotchWindowTests.EmptyWindow_ReturnsEmptyArraysWithZeroStartIndex | commit8d088291.Vega.BL.WaferAlignment.Tests.ExtractNotchWindowTests.EmptyWindow_ReturnsEmptyArraysWithZeroStartIndex | specified behavior | matched |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\daily\analysis\individual-20261022\commit-8d088291-logic\20261023T080431Z_wi0_63e3555`

