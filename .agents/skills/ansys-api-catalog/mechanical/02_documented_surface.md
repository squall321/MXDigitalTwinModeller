# Mechanical ACT API — Documented Surface (from XML reference)

Extracted from `Ansys.ACT.WB1.xml`. Status: DOCUMENTED — cross-reference with [01_used_in_project.md](01_used_in_project.md) for verified-working entries.

## Ansys.ACT.Automation.Mechanical.Model

### Add* methods (analyses)
- `Model.AddStaticStructuralAnalysis() → Analysis`
- `Model.AddTransientStructuralAnalysis() → Analysis` ✓ verified
- `Model.AddRigidDynamicsAnalysis() → Analysis`
- `Model.AddHarmonicResponseAnalysis() → Analysis`
- `Model.AddModalAnalysis() → Analysis` ✓ verified
- `Model.AddSubstructureGenerationAnalysis() → Analysis`
- `Model.AddExplicitDynamicsAnalysis() → Analysis`
- `Model.AddSteadyStateThermalAnalysis() → Analysis`
- `Model.AddTransientThermalAnalysis() → Analysis`
- `Model.AddMagnetostaticAnalysis() → Analysis`
- `Model.AddElectricAnalysis() → Analysis`
- `Model.AddDesignAssessmentAnalysis() → Analysis`
- `Model.AddThermalElectricAnalysis() → Analysis`
- `Model.AddModalAcousticAnalysis() → Analysis`
- `Model.AddHarmonicAcousticAnalysis() → Analysis`
- `Model.AddStaticAcousticAnalysis() → Analysis`
- `Model.AddTopologyOptimizationAnalysis() → Analysis`
- `Model.AddEigenvalueBucklingAnalysis() → Analysis`
- `Model.AddResponseSpectrumAnalysis() → Analysis`
- `Model.AddRandomVibrationAnalysis() → Analysis`
- `Model.AddCoupledFieldStatic() → Analysis`
- `Model.AddCoupledFieldTransient() → Analysis`
- `Model.AddCoupledFieldHarmonic() → Analysis`
- `Model.AddCoupledFieldModal() → Analysis`
- `Model.AddLSDynaAnalysis() → Analysis`
- `Model.AddLSDynaRestartAnalysis() → Analysis`
- `Model.AddLSDynaAcousticsAnalysis() → Analysis`
- `Model.AddMotionAnalysis() → Analysis`
- `Model.AddDesignLifeAnalysis() → Analysis`
- `Model.AddForcedResponseAnalysis() → Analysis`

### Add* methods (other children)
- `Model.AddRemotePoint() → RemotePoint`
- `Model.AddNamedSelection() → NamedSelection` ✓ verified
- `Model.AddNamedSelectionFromSelectedConnections() → NamedSelection`
- `Model.AddPartTransform() → PartTransform`
- `Model.AddChart() → Chart`
- `Model.AddCondensedGeometry() → CondensedGeometry`
- `Model.AddMeasures() → Measures`
- `Model.AddConnections() → Connections`
- `Model.AddCrossSections() → CrossSections`
- `Model.AddConstructionGeometry() → ConstructionGeometry`
- `Model.AddMeshWorkflowGroup() → MeshWorkflowGroup`
- `Model.AddAMProcess() → AMProcess`
- `Model.AddFracture() → Fracture`
- `Model.AddGeometryImportGroup() → GeometryImportGroup`
- `Model.AddTableGroup(bool) → TableGroup`
- `Model.AddParameterVariableGroup() → ParameterVariableGroup`
- `Model.AddMeshEdit() → MeshEdit`
- `Model.AddMeshNumbering() → MeshNumbering`
- `Model.AddSymmetry() → Symmetry`
- `Model.AddVirtualTopology() → VirtualTopology`
- `Model.AddCoSimulationPin() → CoSimulationPin`
- `Model.AddImagePlane() → ImagePlane`
- `Model.AddTreeGroupingFolder(IEnumerable) → TreeGroupingFolder`
- `Model.AddCompositeFailureCriteria() → CompositeFailureCriteria`
- `Model.AddFatigueCombination() → FatigueCombination`
- `Model.AddFeatureDetection() → FeatureDetection`
- `Model.AddPythonCodeEventBased() → PythonCodeEventBased`
- `Model.AddPythonResult() → PythonResult`
- `Model.AddSolutionCombination() → SolutionCombination`
- `Model.AddComment() → Comment`
- `Model.AddFigure() → Figure`
- `Model.AddImage(string filePath) → Image`

### Other methods
- `Model.UpdateGeometryFromSource()`

### Properties
- `Model.Environments` — analyses collection
- `Model.MeshWorkflowGroup`
- `Model.NamedSelections`
- `Model.CondensedGeometry`
- `Model.ConstructionGeometry`
- `Model.Connections` ✓ verified
- `Model.CoordinateSystems`
- `Model.CrossSections`
- `Model.FeatureDetection`
- `Model.Fracture`
- `Model.GeometryImportGroup`
- `Model.Materials` ✓ verified
- `Model.Measures`
- `Model.MeshEdit`
- `Model.Mesh` ✓ verified
- `Model.MeshNumbering`
- `Model.PartTransformGroup`
- `Model.Geometry` ✓ verified
- `Model.RemotePoints`
- `Model.Symmetry`
- `Model.VirtualTopology`
- `Model.Analyses` ✓ verified
- `Model.DataModelObjectCategory` ✓ verified
- `Model.Children` ✓ verified
- `Model.Comments`
- `Model.Figures`
- `Model.Images`

---

## Ansys.ACT.Automation.Mechanical.Geometry

### Properties
- `Geometry.InternalObject`
- `Geometry.TemporaryDirectory`
- `Geometry.Source`
- `Geometry.Type`
- `Geometry.Elements`
- `Geometry.Average`
- `Geometry.Maximum`
- `Geometry.Minimum`
- `Geometry.StandardDeviation`
- `Geometry.Nodes`
- `Geometry.ActiveBodies`
- `Geometry.Bodies`
- `Geometry.ScaleFactorValue`
- `Geometry.Tolerance2D`

---

## Ansys.ACT.Automation.Mechanical.GeometryImport

### Methods
- `GeometryImport.GetGeometryURI() → string` — geometry URI of most recent import
- `GeometryImport.GetFormat()` — format of recent geometry import
- `GeometryImport.GetPreferences() → GeometryImportPreferences`
- `GeometryImport.Import(filePath, GeometryImportPreference.Format, GeometryImportPreferences)`
- `GeometryImport.PrimarySourceRefresh(string)`
- `GeometryImport.JScriptImportEmulation(string)` — emulates `doImportGeomFromProject`

### Properties
- `GeometryImport.Parts` — parts created by most recent import
- `GeometryImport.CanRemove`
- `GeometryImport.PreAction` / `PostAction`
- `GeometryImport.ComObject` — cast InternalObject to OLE Automation interface
- `GeometryImport.GeometryImportDataStorage`
- `GeometryImport.PartIdCollection`

---

## Ansys.ACT.Automation.Mechanical.Body

### Properties
- `Body.Hidden`
- `Body.ConfigurationId`
- `Body.ResultMeshId`
- `Body.ReferenceTemperature`
- `Body.ThicknessMode`
- `Body.Dimension`
- `Body.Assignment`
- `Body.CrossSectionName`
- `Body.ModelType`
- `Body.GeometryType`
- `Body.SuppressedParameterized`
- `Body.Transparency` (0.0 = invisible, 1.0 = visible)
- `Body.Color`
- `Body.InternalObject`
- `Body.BarScaleFactor`
- `Body.BendingScaleFactor`
- `Body.Suppressed` (read/write) ✓ verified
- `Body.Material` ✓ verified
- `Body.Name` ✓ verified
- `Body.Id` ✓ verified

---

## Ansys.ACT.Automation.Mechanical.NamedSelection

### Methods
- `NamedSelection.ExportToTextFile(string)`
- `NamedSelection.ExportNamedSelectionToCDBFile(string)`
- `NamedSelection.Generate()` ✓ verified
- `NamedSelection.CreateNodalNamedSelection() → NamedSelection`
- `NamedSelection.SendToMeshWorkflowByType(WorkflowType) → MeshWorkflowGroup`
- `NamedSelection.Delete()` ✓ verified
- `NamedSelection.GetChildren<T>(bool, IList<T>)`
- `NamedSelection.GetChildren(DataModelObjectCategory, bool, IList)` ✓ verified
- `NamedSelection.WalkDataModelObjects(bool, IDataModelObjectVisitor)`
- `NamedSelection.AddComment() → Comment`
- `NamedSelection.AddFigure() → Figure`
- `NamedSelection.AddImage(string) → Image`

### Properties
- `NamedSelection.Worksheet`
- `NamedSelection.Location` ✓ verified
- `NamedSelection.GenerationCriteria` — generation criteria for the selection
- `NamedSelection.Protected` — corresponding contact entities meshing protection
- `NamedSelection.InternalObject`
- `NamedSelection.CrackFrontNumber`
- `NamedSelection.LSDynaUserId`
- `NamedSelection.TotalSelection`
- `NamedSelection.Suppressed` ✓ verified
- `NamedSelection.RelativeTolerance`
- `NamedSelection.ZeroTolerance`
- `NamedSelection.ScopingMethod` ✓ verified
- `NamedSelection.SendAs`
- `NamedSelection.ToleranceType`
- `NamedSelection.Type`
- `NamedSelection.IncludeProgramControlledInflation`
- `NamedSelection.PreserveDuringSolve`
- `NamedSelection.SendToSolver`
- `NamedSelection.Visible`
- `NamedSelection.Name` ✓ verified
- `NamedSelection.Children` ✓ verified

---

## Ansys.ACT.Automation.Mechanical.Analysis

### Add* (loads / supports / boundary conditions — structural & thermal)
- `Analysis.AddTemperature(DataRepresentation) → Temperature`
- `Analysis.AddConvection(DataRepresentation)`
- `Analysis.AddThermalCondition(DataRepresentation)` / `Analysis.AddThermalCondition() → ThermalCondition`
- `Analysis.AddPressure(DataRepresentation) → Pressure` / `Analysis.AddPressure() → Pressure`
- `Analysis.AddInitialVelocity() → InitialVelocity`
- `Analysis.AddSystemCouplingRegion() → SystemCouplingRegion`
- `Analysis.AddBoltPretension() → BoltPretension`
- `Analysis.AddOptimizationRegion() → OptimizationRegion`
- `Analysis.AddAcousticTemperature() → ThermalCondition`
- `Analysis.AddSourceConductor() → SourceConductor`
- `Analysis.AddImportedLoadResultFile() → ImportedLoadGroup`
- `Analysis.AddImportedLoadMAPDLResultsFile() → ImportedLoadGroup`
- `Analysis.AddImportedLoadFluidsResultsFile() → ImportedLoadGroup`
- `Analysis.AddImportedLoadExternalData() → ImportedLoadGroup`
- `Analysis.AddImportedRemoteLoadsGroup() → LoadGroup`
- `Analysis.AddAcceleration() → Acceleration`
- `Analysis.AddBearingLoad() → BearingLoad`
- `Analysis.AddBodyControl() → BodyControl`
- `Analysis.AddCommandSnippet() → CommandSnippet`
- `Analysis.AddCompressionOnlySupport() → CompressionOnlySupport`
- `Analysis.AddConstraintEquation() → ConstraintEquation`
- `Analysis.AddContactStepControl()`
- `Analysis.AddCoupling()`
- `Analysis.AddVoltageCoupling()`
- `Analysis.AddCurrent() → Current`
- `Analysis.AddCylindricalSupport() → CylindricalSupport`
- `Analysis.AddDetonationPoint()`
- `Analysis.AddDisplacement() → Displacement`
- `Analysis.AddEarthGravity() → EarthGravity`
- `Analysis.AddElasticSupport() → ElasticSupport`
- `Analysis.AddElectricCharge()`
- `Analysis.AddElementBirthAndDeath()`
- `Analysis.AddEMTransducer()`
- `Analysis.AddFixedRotation() → FixedRotation`
- `Analysis.AddFixedSupport() → FixedSupport`
- `Analysis.AddFluidPenetrationPressure() → FluidPenetrationPressure`
- `Analysis.AddFluidSolidInterface() → FluidSolidInterface`
- `Analysis.AddForce() → Force` ✓ verified
- `Analysis.AddFrictionlessSupport() → FrictionlessSupport`
- `Analysis.AddGeneralizedPlaneStrain()`
- `Analysis.AddGeometryBasedAdaptivity()`
- `Analysis.AddHeatFlow() → HeatFlow`
- `Analysis.AddHeatFlux() → HeatFlux`
- `Analysis.AddHydrostaticPressure() → HydrostaticPressure`
- `Analysis.AddImpedanceBoundary()`
- `Analysis.AddImportedCFDPressure()`
- `Analysis.AddInternalHeatGeneration() → InternalHeatGeneration`
- `Analysis.AddJointLoad() → JointLoad`
- `Analysis.AddLimitBoundary()`
- `Analysis.AddLinePressure() → LinePressure`
- `Analysis.AddLoadApplication() → LoadApplication`
- `Analysis.AddMagneticFluxParallel()`
- `Analysis.AddMassFlowRate() → MassFlowRate`
- `Analysis.AddMoment() → Moment`
- `Analysis.AddMorphingRegion()`
- `Analysis.AddNodalDisplacement() → NodalDisplacement`
- `Analysis.AddNodalForce() → NodalForce`
- `Analysis.AddNodalOrientation() → NodalOrientation`
- `Analysis.AddNodalPressure() → NodalPressure`
- `Analysis.AddNodalRotation() → NodalRotation`
- `Analysis.AddNonlinearAdaptiveRegion()`
- `Analysis.AddObjective()`
- `Analysis.AddPerfectlyInsulated() → PerfectlyInsulated`
- `Analysis.AddPhysicsRegion() → PhysicsRegion`
- `Analysis.AddPipeIdealization() → PipeIdealization`
- `Analysis.AddPipePressure() → PipePressure`
- `Analysis.AddPipeTemperature() → PipeTemperature`
- `Analysis.AddPlasticHeating()`
- `Analysis.AddPSDAcceleration()` / `AddPSDDisplacement()` / `AddPSDGAcceleration()` / `AddPSDPressure() → PSDPressure` / `AddPSDVelocity()`
- `Analysis.AddPythonCodeEventBased()`
- `Analysis.AddRadiation() → Radiation`
- `Analysis.AddRemoteDisplacement() → RemoteDisplacement`
- `Analysis.AddRemoteForce() → RemoteForce`
- `Analysis.AddRotatingForce()`
- `Analysis.AddRotationalAcceleration() → RotationalAcceleration`
- `Analysis.AddRotationalVelocity() → RotationalVelocity`
- `Analysis.AddRSAcceleration()` / `AddRSDisplacement()` / `AddRSVelocity()`
- `Analysis.AddSimplySupported()`
- `Analysis.AddSubstructureGenerationCondensedPart()`
- `Analysis.AddSurfaceChargeDensity()`
- `Analysis.AddVelocity() → Velocity`
- `Analysis.AddViscoelasticHeating()`
- `Analysis.AddVoltage() → Voltage`
- `Analysis.AddVoltageGround()`
- `Analysis.AddVolumeChargeDensity()`
- `Analysis.AddComment() → Comment`
- `Analysis.AddFigure() → Figure`
- `Analysis.AddImage(string) → Image`

### Acoustic-specific Add* (Acoustic toolbox)
- `Analysis.AddAcousticAbsorptionElement()`, `AddAcousticAbsorptionSurface()`, `AddAcousticDiffuseSoundField()`, `AddAcousticFarFieldRadationSurface()`, `AddAcousticFreeSurface()`, `AddAcousticImpedanceBoundary()`, `AddAcousticImpedanceSheet()`, `AddAcousticIncidentWaveSource()`, `AddAcousticLowReducedFrequency()`, `AddAcousticMassSource()`, `AddAcousticMassSourceRate()`, `AddAcousticPort()`, `AddAcousticPortInDuct()`, `AddAcousticPressure()`, `AddAcousticRadiationBoundary()`, `AddAcousticRigidWall()`, `AddAcousticStaticPressure()`, `AddAcousticSurfaceAcceleration()`, `AddAcousticSurfaceVelocity()`, `AddAcousticSymmetryPlane()`, `AddAcousticThermoViscousBLIBoundary()`, `AddAcousticTransferAdmittanceMatrix()`

### Optimization Add* (Topology Optimization)
- `Analysis.AddComplexityIndexConstraint()`, `AddComplianceConstraint()`, `AddCriterionConstraint()`, `AddCyclicManufacturingConstraint()`, `AddDisplacementConstraint()`, `AddDynamicComplianceConstraint()`, `AddExtrusionManufacturingConstraint()`, `AddGlobalVonMisesStressConstraint()`, `AddHousingConstraint()`, `AddLocalVonMisesStressConstraint()`, `AddMassConstraint()`, `AddMemberSizeManufacturingConstraint()`, `AddMomentOfInertiaConstraint()`, `AddNaturalFrequencyConstraint()`, `AddPatternRepetitionConstraint()`, `AddPullOutDirectionManufacturingConstraint()`, `AddReactionForceConstraint()`, `AddSymmetryManufacturingConstraint()`, `AddTemperatureConstraint()`, `AddThermalComplianceConstraint()`, `AddUniformConstraint()`, `AddVolumeConstraint()`, `AddCenterOfGravityConstraint()`, `AddAMOverhangConstraint()`

### Properties
- `Analysis.CellId`
- `Analysis.SystemCaption`
- `Analysis.InitialConditions`
- `Analysis.ResultFileName`
- `Analysis.InternalObject`
- `Analysis.EnvironmentTemperature`
- `Analysis.AMProcessSimulation`
- `Analysis.AnalysisType`
- `Analysis.PhysicsType`
- `Analysis.Acoustics`
- `Analysis.Electric`
- `Analysis.GenerateInputOnly`
- `Analysis.Structural`
- `Analysis.Thermal`
- `Analysis.AnalysisSettings` ✓ verified
- `Analysis.Solution` ✓ verified
- `Analysis.DataModelObjectCategory`
- `Analysis.Children` ✓ verified
- `Analysis.Comments`
- `Analysis.Figures`
- `Analysis.Images`
- `Analysis.Name` ✓ verified
- `Analysis.Activate()` (method) ✓ verified

---

## Ansys.ACT.Automation.Mechanical.AnalysisSettings.ANSYSAnalysisSettings

### Per-step Set/Get methods (first arg = step number, 1-based)
- `SetStepEndTime(uint, Quantity)` / `GetStepEndTime(uint)` ✓ verified
- `SetAutomaticTimeStepping(uint, AutomaticTimeStepping)` / `GetAutomaticTimeStepping(uint)`
- `SetInitialTimeStep(uint, Quantity)` / `GetInitialTimeStep(uint)` ✓ verified
- `SetMinimumTimeStep(uint, Quantity)` / `GetMinimumTimeStep(uint)` ✓ verified
- `SetMaximumTimeStep(uint, Quantity)` / `GetMaximumTimeStep(uint)` ✓ verified
- `SetInitialSubsteps(uint, ...)` / `GetInitialSubsteps(uint)`
- `SetMinimumSubsteps(uint, ...)` / `GetMinimumSubsteps(uint)`
- `SetMaximumSubsteps(uint, ...)` / `GetMaximumSubsteps(uint)`
- `SetNumberOfSubSteps(uint, ...)` / `GetNumberOfSubSteps(uint)`
- `SetTimeStep(uint, Quantity)` / `GetTimeStep(uint)`
- `SetCarryOverTimeStep(uint, bool)` / `GetCarryOverTimeStep(uint)`
- `SetDefineBy(uint, TimeStepDefineByType)` / `GetDefineBy(uint)`
- `SetTimeIntegration(uint, bool)` / `GetTimeIntegration(uint)`
- `SetStructuralOnly(uint, bool)` / `GetStructuralOnly(uint)`
- `SetThermalOnly(uint, bool)` / `GetThermalOnly(uint)`
- `SetStoreResultsAt(uint, ...)` / `GetStoreResultsAt(uint)`
- `SetStoreResulsAtValue(uint, ...)` / `GetStoreResulsAtValue(uint)` — note official typo "Resuls"
- `SetStepName(uint, string)` / `GetStepName(uint)`
- `SetAMStepType(uint, ...)` / `GetAMStepType(uint)`
- `SetCreepEffects(uint, bool)` / `GetCreepEffects(uint)`
- `SetCreepLimitRatio(uint, double)` / `GetCreepLimitRatio(uint)`

### Per-step Convergence Set/Get methods
- `SetForceConvergenceType(uint, ConvergenceToleranceType)` / `GetForceConvergenceType(uint)`
- `SetForceConvergenceTolerance(uint, double)` / `GetForceConvergenceTolerance(uint)`
- `SetForceConvergenceValue(uint, Quantity)` / `GetForceConvergenceValue(uint)`
- `SetForceConvergenceMinimumReference(uint, Quantity)` / `GetForceConvergenceMinimumReference(uint)`
- `SetMomentConvergenceType` / `SetMomentConvergenceTolerance` / `SetMomentConvergenceValue` / `SetMomentConvergenceMinimumReference` (+ Get versions)
- `SetDisplacementConvergenceType/Tolerance/Value/MinimumReference` (+ Get)
- `SetRotationConvergenceType/Tolerance/Value/MinimumReference` (+ Get)
- `SetTemperatureConvergenceType/Tolerance/Value/InputValue/MinimumReference` (+ Get)
- `SetHeatConvergenceType/Tolerance/Value/MinimumReference` (+ Get)
- `SetVoltageConvergenceType/Tolerance/Value/MinimumReference` (+ Get)
- `SetChargeConvergenceType/Tolerance/Value/MinimumReference` (+ Get)
- `SetEnergyConvergenceType/Tolerance/Value/MinimumReference` (+ Get)
- `SetCurrentConvergenceType/Tolerance/Value/MinimumReference` (+ Get)
- `SetEmagAMPSConvergenceType/Tolerance/Value/MinimumReference` (+ Get)
- `SetEmagCSGConvergenceType/Tolerance/Value/MinimumReference` (+ Get)

### Per-step Stabilization / LineSearch Set/Get methods
- `SetLineSearch(uint, ...)` / `GetLineSearch(uint)`
- `SetStabilization(uint, ...)` / `GetStabilization(uint)`
- `SetStabilizationMethod(uint, ...)` / `GetStabilizationMethod(uint)`
- `SetStabilizationEnergyDissipationRatio(uint, double)` / `GetStabilizationEnergyDissipationRatio(uint)`
- `SetStabilizationDampingFactor(uint, double)` / `GetStabilizationDampingFactor(uint)`
- `SetStabilizationFirstSubstepOption(uint, ...)` / `GetStabilizationFirstSubstepOption(uint)`
- `SetStabilizationForceLimit(uint, double)` / `GetStabilizationForceLimit(uint)`

### Modal-specific properties
- `ModalRangeMaximum` ✓ verified
- `ModalRangeMinimum`
- `ModalNumberOfPoints`
- `ModalFrequencyRange`
- `MaximumModesToFind` ✓ verified

### Transient/Step properties
- `StepName`
- `NumberOfSteps`
- `CurrentStepNumber`
- `CurrentStepNumberHarmonic`
- `StepEndTime`
- `TimeStep`
- `MaximumTimeStep`
- `MinimumTimeStep`
- `MaximumSubsteps`
- `MinimumSubsteps`
- `MaximumIteration`
- `MaximumPointsToSavePerStep`
- `MaximumTotalFilesToSave` (0..999)

### Solver / Damping / Nonlinear properties
- `LargeDeflection`
- `AggressiveRemeshing`
- `SpinSoftening`
- `UserDefinedFrequencySteps`
- `VariationalTechnology`
- `SkipExpansion`
- `OnDemandExpansion`
- `CombineDistributedResultFiles`
- `MultipleRPMs`
- `CurrentRPMNumber`
- `CoriolisEffect` (Stationary Reference Frame)
- `OutputSelection`
- `NamedSelection` (for output controls)
- `InternalObject`
- `MassCoefficient` (Rayleigh α)
- `StiffnessCoefficient` (Rayleigh β)
- `DampingRatio`
- `ConstantDampingRatio`
- `StructuralDampingCoefficient`
- `MinimumElementSize` (geometry-based adaptivity)
- `SolverTolerance`
- `SolverUnitSystem`
- `SolverPivotChecking`
- `SolverType`
- `SolverUnits`

### Additive Manufacturing properties
- `AMStepType`
- `AMSubstepsToApplyHeats`
- `AMSubstepsBetweenHeating`
- `AMCooldownNumberOfSubsteps`
- `CooldownTimeType` / `CooldownTime`
- `LayersToBuild`
- `ReferenceTemperatureType` / `ReferenceTemperature`
- `RelaxationTemperatureType`
- `NumberOfRestartPoints`
- `CurrentRestartPoint`

---

## Ansys.ACT.Automation.Mechanical.BoundaryConditions.Force

### Properties
- `Force.Direction`
- `Force.RpmVarying`
- `Force.LoadStepVarying`
- `Force.RpmSelection`
- `Force.InternalObject`
- `Force.NumberOfSegments`
- `Force.LoadVectorNumber` (+ Imaginary variants)
- `Force.LoadVectorNumberX` / `LoadVectorNumberXImaginary`
- `Force.LoadVectorNumberY` / `LoadVectorNumberYImaginary`
- `Force.LoadVectorNumberZ` / `LoadVectorNumberZImaginary`
- `Force.StepSelection`
- `Force.XComponent` ✓ verified
- `Force.XComponentImag`
- `Force.YComponent` / `YComponentImag` ✓ verified
- `Force.ZComponent` / `ZComponentImag` ✓ verified
- `Force.Location` (inherited) ✓ verified
- `Force.DefineBy` (inherited) ✓ verified

---

## Ansys.ACT.Automation.Mechanical.BoundaryConditions.Pressure

### Properties
- `Pressure.Direction`
- `Pressure.Magnitude` (read-only)
- `Pressure.NumberOfSegments`
- `Pressure.LoadVectorNumber` (+ Imaginary variants)
- `Pressure.XComponent` / `XComponentImag`
- `Pressure.YComponent` / `YComponentImag`
- `Pressure.ZComponent` / `ZComponentImag`

---

## Ansys.ACT.Automation.Mechanical.BoundaryConditions.Displacement

### Properties
- `Displacement.BoundaryCondition`
- `Displacement.RpmVarying`
- `Displacement.LoadStepVarying`
- `Displacement.RpmSelection`
- `Displacement.InternalObject`
- `Displacement.NumberOfSegments`
- `Displacement.StepSelection`
- `Displacement.XComponent` / `XComponentImag`
- `Displacement.YComponent` / `YComponentImag`
- `Displacement.ZComponent` / `ZComponentImag`
- `Displacement.Distance`
- `Displacement.MagnitudeImag`
- `Displacement.PhaseAngle`
- `Displacement.XPhaseAngle`

---

## Ansys.ACT.Automation.Mechanical.BoundaryConditions.RemoteForce

### Properties
- `RemoteForce.Direction`
- `RemoteForce.BeamMaterial`
- `RemoteForce.NumberOfSegments`
- `RemoteForce.StepSelection`
- `RemoteForce.XComponent` / `XComponentImag`
- `RemoteForce.YComponent` / `YComponentImag`
- `RemoteForce.ZComponent` / `ZComponentImag`
- `RemoteForce.HarmonicIndex`
- `RemoteForce.SectorNumber`
- `RemoteForce.Magnitude` (read-only)

---

## Ansys.ACT.Automation.Mechanical.Connections.Connections

### Add* methods
- `Connections.AddContactRegion() → ContactRegion` ✓ verified
- `Connections.AddContactTool() → ContactTool`
- `Connections.AddSpotWeldGroup() → SpotWeldGroup`
- `Connections.AddSpotWeld() → SpotWeld`
- `Connections.AddInterStage() → InterStage`
- `Connections.AddJoint() → Joint`
- `Connections.AddBodyInteraction() → BodyInteraction`
- `Connections.SearchConnectionsForDuplicatePairs()`

---

## Ansys.ACT.Automation.Mechanical.Connections.ContactRegion

### Methods
- `ContactRegion.FlipContactTarget()`
- `ContactRegion.SetDefaultAPDLNames()`
- `ContactRegion.SaveContactRegionSettings(string)`
- `ContactRegion.LoadContactRegionSettings(string)`
- `ContactRegion.ResetToDefault()`
- `ContactRegion.PromoteToNamedSelection()`
- `ContactRegion.PromoteToRemotePoint()`

### Properties
- `ContactRegion.ContactAPDLName` / `TargetAPDLName`
- `ContactRegion.SourceLocation` ✓ verified
- `ContactRegion.TargetLocation` ✓ verified
- `ContactRegion.AutomaticNormalStiffness`
- `ContactRegion.BeamMaterialName`
- `ContactRegion.BeamBeamDetection`
- `ContactRegion.GloballyAvailable`
- `ContactRegion.ConstraintType`
- `ContactRegion.Material`
- `ContactRegion.StabilizationDampingFactor`
- `ContactRegion.DecayConstant`
- `ContactRegion.DynamicCoefficient`
- `ContactRegion.ElasticSlipToleranceFactor`
- `ContactRegion.FrictionCoefficient` ✓ verified
- `ContactRegion.InitialClearanceFactor`
- `ContactRegion.NormalForceExponent`
- `ContactRegion.NormalStiffnessFactor`
- `ContactRegion.NormalStressExponent`
- `ContactRegion.PenetrationToleranceFactor`
- `ContactRegion.PinballFactor`
- `ContactRegion.PressureAtZeroPenetrationFactor`
- `ContactRegion.RestitutionFactor`
- `ContactRegion.ShearForceExponent`
- `ContactRegion.ShearStressExponent`
- `ContactRegion.ContactBodies`
- `ContactRegion.TargetBodies`
- `ContactRegion.AutomaticDetectionValue`
- `ContactRegion.BeamRadius`
- `ContactRegion.ThreadAngle`
- `ContactRegion.MeanPitchDiameter`
- `ContactRegion.PitchDistance`
- `ContactRegion.BondedMaximumOffset`
- `ContactRegion.ElasticSlipToleranceValue`
- `ContactRegion.ElectricCapacitanceValue`
- `ContactRegion.ElectricConductanceValue`
- `ContactRegion.InitialClearanceValue`
- `ContactRegion.NormalForceLimit`
- `ContactRegion.NormalStiffnessValue`
- `ContactRegion.ContactType` ✓ verified
- `ContactRegion.ContactFormulation`
- `ContactRegion.InterfaceTreatment`
- `ContactRegion.Behavior`
- `ContactRegion.TrimContact`
- `ContactRegion.Suppressed` ✓ verified

---

## Ansys.ACT.Automation.Mechanical.MeshControls.Mesh

### Add* methods
- `Mesh.AddNodeMergeGroup() → NodeMergeGroup`
- `Mesh.AddNodeMerge() → NodeMerge`
- `Mesh.AddAutomaticMethod() → AutomaticMethod`
- `Mesh.AddMeshConnectionGroup() → MeshConnectionGroup`
- `Mesh.AddContactMatchGroup() → ContactMatchGroup`
- `Mesh.AddManualMeshConnection()`
- `Mesh.AddPullExtrude()`
- `Mesh.AddPullRevolve()`
- `Mesh.AddPullSurfaceCoating()`
- `Mesh.AddDirectMorph()`
- `Mesh.AddDeviation()`
- `Mesh.AddWasher()`
- `Mesh.AddWeld()`
- `Mesh.AddRepairTopology()`
- `Mesh.AddConnect()`
- `Mesh.AddFeatureSuppress()`
- `Mesh.AddGeometryFidelity()`
- `Mesh.AddMeshCopy()`
- `Mesh.AddTopologySuppressControl()`
- `Mesh.AddGasket()`
- `Mesh.AddMeshLayers()`
- `Mesh.AddContactSizing()`
- `Mesh.AddFaceMeshing()`
- `Mesh.AddInflation() → Inflation`
- `Mesh.AddMatchControl()`
- `Mesh.AddPinch()`
- `Mesh.AddRefinement() → Refinement`
- `Mesh.AddSizing() → Sizing`

### Other methods
- `Mesh.PreviewWelds()`
- `Mesh.ShowOverlappingFaces()`
- `Mesh.ShowUnconnectedFacesNearEdges()`
- `Mesh.ClearGeneratedData()`
- `Mesh.GenerateMesh()` ✓ verified

### Properties
- `Mesh.Worksheet`
- `Mesh.ElementSize`
- `Mesh.RigidBodyFaceMeshType`
- `Mesh.RigidBodyBehavior`
- `Mesh.MinimizeNumTriangles`
- `Mesh.GlobalUseCustomTargetLimit`
- `Mesh.LocalConnectionOption` / `LocalConnectionTolerance`
- `Mesh.ConnectionTolerance` / `ConnectionToleranceList`
- `Mesh.UseAdvancedSizeFunction`
- `Mesh.Method`
- `Mesh.UseAutomaticInflation`
- `Mesh.AutomaticMeshBasedDefeaturing`
- `Mesh.Beam3` / `Mesh.Beam4` / `Mesh.BeamElements`
- `Mesh.CheckMeshQuality`
- `Mesh.CollisionAvoidance`
- `Mesh.ConnectionSize`
- `Mesh.CornerNodes`
- `Mesh.Elements`
- `Mesh.GrowthRate` / `GrowthRateType`
- `Mesh.GasketElements`
- `Mesh.Hex20` / `Hex8`
- `Mesh.HoleRemovalTolerance`
- `Mesh.InflationAlgorithm`
- `Mesh.Nodes`

---

## Ansys.ACT.Automation.Mechanical.MeshControls.Sizing

### Properties
- `Sizing.BodyOfInfluence`
- `Sizing.BiasGrowthRate`
- `Sizing.NumberOfDivisions`
- `Sizing.GrowthRate`
- `Sizing.BiasFactor`
- `Sizing.ElementSize`
- `Sizing.SphereRadius`
- `Sizing.DefeatureSize`
- `Sizing.LocalMinimumSize`
- `Sizing.ProximityGapFactor`
- `Sizing.ProximityMinimumSize`
- `Sizing.CurvatureNormalAngle`
- `Sizing.OriginX` / `OriginY` / `OriginZ`
- `Sizing.BiasOption`

---

## Ansys.ACT.Automation.Mechanical.MeshControls.AutomaticMethod

### Properties
- `AutomaticMethod.AggressiveInflateOption`
- `AutomaticMethod.AggressiveTetImprovement`
- `AutomaticMethod.ControlMessages`
- `AutomaticMethod.CornerAngle`
- `AutomaticMethod.DefeatureLayerVolume`
- `AutomaticMethod.ElementMidsideNodes`
- `AutomaticMethod.ElementOrder`
- `AutomaticMethod.GenerateLayersUsingFacets`
- `AutomaticMethod.InflateRelativeTolerance`
- `AutomaticMethod.LayerHeight`
- `AutomaticMethod.LayerStart`
- `AutomaticMethod.MeshInCenter`
- `AutomaticMethod.Method`
- `AutomaticMethod.OverlappingAngle`
- `AutomaticMethod.ProjectCornersToTop`
- `AutomaticMethod.RelativeTolerance`
- `AutomaticMethod.RepairFacets`

---

## Ansys.ACT.Automation.Mechanical.MeshControls.Inflation

### Properties
- `Inflation.BoundaryLocation`
- `Inflation.GrowthRate`
- `Inflation.InflationAlgorithm`
- `Inflation.InflationOption`
- `Inflation.AspectRatio`
- `Inflation.MaximumLayers`
- `Inflation.NumberOfLayers`
- `Inflation.TransitionRatio`
- `Inflation.FirstLayerHeight`

---

## Ansys.ACT.Automation.Mechanical.MeshControls.Refinement

### Properties
- `Refinement.NumberOfRefinements`
- `Refinement.NamedSelection`
- `Refinement.Location`
- `Refinement.DataModelObjectCategory`

---

## Ansys.ACT.Automation.Mechanical.Materials

### Methods
- `Materials.AddMaterialCombination() → MaterialCombination`
- `Materials.AddImportedTraceExternalData() → ImportedTraceGroup`
- `Materials.Import(string, ImportFormat, ImportSettings)`
- `Materials.AddMaterialAssignment() → MaterialAssignment`
- `Materials.AddMaterialPlot() → MaterialPlot`
- `Materials.RefreshMaterials()`
- `Materials.AddMaterial()` ✓ verified

### Properties
- `Materials.InternalObject`
- `Materials.MaterialAssignments`
- `Materials.MaterialCount`
- `Materials.DataModelObjectCategory`
- `Materials.Children`
- `Materials.Comments`
- `Materials.Figures`

---

## Ansys.ACT.Automation.Mechanical.Material

### Methods
- `Material.CreateMaterialAssignment() → MaterialAssignment`
- `Material.AddMaterialAssignment() → MaterialAssignment`
- `Material.GetEngineeringDataMaterial()` — returns C# Engineering Data Object
- `Material.GetAsDictionary()` — returns material as Python dictionary
- `Material.GetMaterialAsDictionary(object)`
- `Material.SetPropertyByName(string, Quantity)` ✓ verified (e.g., `"Young's Modulus"`, `"Poisson's Ratio"`, `"Density"`)

### Properties
- `Material.MaterialModels` — collection of material models
- `Material.AssignedBodies`
- `Material.DataModelObjectCategory`
- `Material.Children`
- `Material.Comments`
- `Material.Figures`

---

## Ansys.ACT.Automation.Mechanical.Solution

### Solve / Evaluate methods
- `Solution.Solve(bool blocking)` ✓ verified
- `Solution.EvaluateAllResults()` ✓ verified

### Add* (tools / probes / bulk)
- `Solution.AddContactTool() → ContactTool`
- `Solution.AddBoltTool() → BoltTool`
- `Solution.AddForceSummationProbe() → ForceSummationProbe`
- `Solution.AddTorqueProbe() → TorqueProbe`
- `Solution.AddResponsePSDTool() → ResponsePSDTool`
- `Solution.AddForceReactionsForContactRegions(IEnumerable<int>)` — bulk
- `Solution.AddMomentReactionsForContactRegions(IEnumerable<int>)` — bulk
- `Solution.AddReactionsForContactRegions(IEnumerable<int>)` — bulk
- `Solution.AddKrylovResidualNorm()`
- `Solution.AddLineChart2D() → LineChart2D`

### Add* (structural / deformation / strain / stress) — most-used set
- `Solution.AddTotalDeformation()` ✓ verified
- `Solution.AddDirectionalDeformation()`
- `Solution.AddEquivalentStress()` ✓ verified
- `Solution.AddMaximumPrincipalStress()` / `AddMinimumPrincipalStress()` / `AddMiddlePrincipalStress()`
- `Solution.AddMaximumShearStress()` / `AddNormalStress()` / `AddShearStress()` / `AddStressIntensity()`
- `Solution.AddEquivalentElasticStrain()` ✓ verified
- `Solution.AddEquivalentPlasticStrain()`
- `Solution.AddMaximumPrincipalElasticStrain()` / `AddMiddlePrincipalElasticStrain()` / `AddMinimumPrincipalElasticStrain()`
- `Solution.AddMaximumShearElasticStrain()` / `AddShearElasticStrain()` / `AddNormalElasticStrain()`
- `Solution.AddElasticStrainFrequencyResponse()` / `AddElasticStrainIntensity()` / `AddElasticStrainPhaseResponse()`
- `Solution.AddDeformationFrequencyResponse()` / `AddDeformationPhaseResponse()` / `AddDeformationProbe()`
- `Solution.AddStressFrequencyResponse()` / `AddStressPhaseResponse()` / `AddStressProbe()` / `AddStressTool()`
- `Solution.AddDirectionalAcceleration()` / `AddDirectionalAccelerationPSD()` / `AddDirectionalAccelerationRS()`
- `Solution.AddDirectionalVelocity()` / `AddDirectionalVelocityPSD()` / `AddDirectionalVelocityRS()`
- `Solution.AddTotalAcceleration()` / `AddTotalVelocity()`
- `Solution.AddVelocityFrequencyResponse()` / `AddVelocityPhaseResponse()` / `AddVelocityProbe()`
- `Solution.AddAccelerationFrequencyResponse()` / `AddAccelerationPhaseResponse()` / `AddAccelerationProbe()`
- `Solution.AddThermalStrain()` (DirectionalThermalStrain)
- `Solution.AddMaximumPrincipalThermalStrain()` / `AddMiddlePrincipalThermalStrain()`

### Add* (thermal / heat)
- `Solution.AddTotalHeatFlux()`
- `Solution.AddDirectionalHeatFlux()`
- `Solution.AddTemperature()`
- `Solution.AddTemperatureProbe()`
- `Solution.AddHeatFluxProbe()`
- `Solution.AddRadiationProbe()`
- `Solution.AddFluidFlowRate()`
- `Solution.AddFluidHeatConductionRate()` / `AddFluidHeatTransportRate()`

### Add* (probes / reactions)
- `Solution.AddBoltPretensionProbe()`
- `Solution.AddBeamProbe()` / `AddBeamTool()`
- `Solution.AddBearingProbe()`
- `Solution.AddContactDistanceProbe()`
- `Solution.AddJointProbe()`
- `Solution.AddSpringProbe()`
- `Solution.AddStrainProbe()`
- `Solution.AddGeneralizedPlaneStrainProbe()`
- `Solution.AddVoltageProbe()`
- `Solution.AddCurrentDensityProbe()`
- `Solution.AddJouleHeatProbe()`
- `Solution.AddElectricFieldProbe()`
- `Solution.AddFlexibleRotationProbe()`
- `Solution.AddAngularAccelerationProbe()` / `AddAngularVelocityProbe()`
- `Solution.AddEnergyProbe()`
- `Solution.AddForceReaction()` / `AddForceReactionFrequencyResponse()`
- `Solution.AddMomentReaction()`
- `Solution.AddReactionProbe()`
- `Solution.AddChargeReactionProbe()` / `AddChargeReactionFrequencyResponse()`
- `Solution.AddEmagReactionProbe()`
- `Solution.AddVoltageFrequencyResponse()`
- `Solution.AddPosition()`
- `Solution.AddRotationProbe()`

### Add* (energies)
- `Solution.AddElementalStrainEnergy()`
- `Solution.AddStructuralStrainEnergy()`
- `Solution.AddThermalStrainEnergy()`
- `Solution.AddStabilizationEnergy()`
- `Solution.AddEnergyContribution()` / `AddEnergyDissipatedPerUnitVolume()`

### Add* (acoustic results)
- `Solution.AddAcousticPressureResult()` / `AddAcousticPressureFrequencyResponse()`
- `Solution.AddAcousticSoundPressureLevel()` / `AddAcousticAWeightedSoundPressureLevel()`
- `Solution.AddAcousticAbsorptionCoefficient()`
- `Solution.AddAcousticDiffuseSoundTransmissionLoss()`
- `Solution.AddAcousticDirectionalVelocityResult()` / `AddAcousticTotalVelocityResult()` / `AddAcousticVelocityFrequencyResponse()`
- `Solution.AddAcousticFarFieldSPL()` / `AddAcousticFarFieldAWeightedSPL()` / `AddAcousticFarFieldMaximumPressure()` / `AddAcousticFarFieldPhase()` / `AddAcousticFarFieldDirectivity()`
- `Solution.AddAcousticFarFieldMaximumScatteredPressure()` / `AddAcousticFarFieldTargetStrength()` / `AddAcousticFarFieldSoundPowerLevel()`
- `Solution.AddAcousticFarFieldSPLMic()` / `AddAcousticFarFieldAWeightedSPLMic()` / `AddAcousticFarFieldMaximumPressureMic()` / `AddAcousticFarFieldPhaseMic()`
- `Solution.AddAcousticReturnLoss()` / `AddAcousticTransmissionLoss()`
- `Solution.AddAcousticKineticEnergy()` / `AddAcousticPotentialEnergy()`
- `Solution.AddAcousticAWeightedSPLFrequencyResponse()` / `AddAcousticSPLFrequencyResponse()`

### Add* (electromagnetic)
- `Solution.AddCurrentDensity()` / `AddDirectionalCurrentDensity()` / `AddTotalCurrentDensity()`
- `Solution.AddElectricPotential()` / `AddElectricVoltage()`
- `Solution.AddDirectionalElectricFieldIntensity()` / `AddTotalElectricFieldIntensity()`
- `Solution.AddDirectionalElectricFluxDensity()` / `AddTotalElectricFluxDensity()`
- `Solution.AddDirectionalElectrostaticForce()` / `AddTotalElectrostaticForce()`
- `Solution.AddDirectionalMagneticFieldIntensity()` / `AddTotalMagneticFieldIntensity()`
- `Solution.AddDirectionalMagneticFluxDensity()` / `AddTotalMagneticFluxDensity()`
- `Solution.AddMagneticPotential()` / `AddMagneticCoenergy()` / `AddMagneticDirectionalForces()` / `AddMagneticTotalForces()` / `AddMagneticError()` / `AddMagneticFluxProbe()`
- `Solution.AddInductance()` / `AddFluxLinkage()`
- `Solution.AddImpedanceFrequencyResponse()` / `AddImpedanceProbe()`
- `Solution.AddFieldIntensityProbe()` / `AddFluxDensityProbe()`
- `Solution.AddJouleHeat()`
- `Solution.AddQualityFactor()`
- `Solution.AddElectromechanicalCouplingCoefficient()`

### Add* (composites)
- `Solution.AddCompositeCriterion()`
- `Solution.AddCompositeFailureTool()`
- `Solution.AddCompositeSamplingPointTool()`
- `Solution.AddMaximumFailureCriteria()`
- `Solution.AddFiberCompressiveDamageVariable()` / `AddFiberCompressiveFailureCriterion()`
- `Solution.AddFiberTensileDamageVariable()` / `AddFiberTensileFailureCriterion()`
- `Solution.AddMatrixCompressiveDamageVariable()` / `AddMatrixCompressiveFailureCriterion()`
- `Solution.AddMatrixTensileDamageVariable()` / `AddMatrixTensileFailureCriterion()`
- `Solution.AddShearDamageVariable()`
- `Solution.AddDamageStatus()`

### Add* (gasket / pipe / specialized)
- `Solution.AddNormalGasketPressure()` / `AddNormalGasketTotalClosure()`
- `Solution.AddShearGasketPressure()` / `AddShearGasketTotalClosure()`
- `Solution.AddDirectionalAxialForce()` / `AddDirectionalBendingMoment()` / `AddDirectionalShearForce()` / `AddDirectionalTorsionalMoment()`
- `Solution.AddTotalAxialForce()` / `AddTotalBendingMoment()` / `AddTotalShearForce()` / `AddTotalTorsionalMoment()`
- `Solution.AddVectorAxialForce()` / `AddVectorBendingMoment()` / `AddVectorShearForce()` / `AddVectorTorsionalMoment()` / `AddVectorDeformation()` / `AddVectorHeatFlux()` / `AddVectorPrincipalElasticStrain()` / `AddVectorPrincipalStress()`
- `Solution.AddShearMomentDiagramMY()` / `AddShearMomentDiagramMZ()` / `AddShearMomentDiagramUY()` / `AddShearMomentDiagramUZ()` / `AddShearMomentDiagramVY()` / `AddShearMomentDiagramVZ()` / `AddShearMomentDiagramMSUM()` / `AddShearMomentDiagramUSUM()` / `AddShearMomentDiagramVSUM()`
- `Solution.AddLinePressureResult()`

### Add* (linearized)
- `Solution.AddLinearizedEquivalentStress()` / `AddLinearizedMaximumPrincipalStress()` / `AddLinearizedMaximumShearStress()` / `AddLinearizedMiddlePrincipalStress()` / `AddLinearizedMinimumPrincipalStress()` / `AddLinearizedNormalStress()` / `AddLinearizedShearStress()` / `AddLinearizedStressIntensity()`
- `Solution.AddBendingStressEquivalent()` / `AddBendingStressIntensity()`
- `Solution.AddMembraneStressEquivalent()` / `AddMembraneStressIntensity()`
- `Solution.AddShellBendingStress()` / `AddShellBottomPeakStress()` / `AddShellMembraneStress()` / `AddShellTopPeakStress()`

### Add* (modal/PSD/RS specific)
- `Solution.AddEquivalentStressPSD()` / `AddEquivalentStressRS()`
- `Solution.AddCampbellDiagram()`
- `Solution.AddResponsePSD()`
- `Solution.AddMCFWaterfallDiagram()`
- `Solution.AddAccelerationWaterfallDiagram()` / `AddDisplacementWaterfallDiagram()` / `AddVelocityWaterfallDiagram()`
- `Solution.AddEquivalentRadiatedPower()` / `AddEquivalentRadiatedPowerLevel()` (+ Waterfall variants)

### Add* (topology/optimization)
- `Solution.AddTopologyDensity()` / `AddTopologyElementalDensity()` / `AddTopologyMultiDensity()`
- `Solution.AddLatticeDensity()` / `AddLatticeElementalDensity()`
- `Solution.AddPrimaryCriterion()`
- `Solution.AddObjective()`
- `Solution.AddShapeFinder()` / `AddShapeFinderElemental()`
- `Solution.AddExpansionSettings()`

### Add* (creep / Mullins)
- `Solution.AddEquivalentCreepStrain()` / `AddEquivalentCreepStrainRST()`
- `Solution.AddEquivalentPlasticStrainRST()` / `AddEquivalentElasticStrainRST()`
- `Solution.AddAccumulatedEquivalentPlasticStrain()`
- `Solution.AddMullinsDamageVariable()` / `AddMullinsMaximumPreviousStrainEnergy()`

### Add* (Newton-Raphson residuals)
- `Solution.AddNewtonRaphsonResidualCharge()` / `AddNewtonRaphsonResidualForce()` / `AddNewtonRaphsonResidualHeat()` / `AddNewtonRaphsonResidualMoment()`

### Add* (Euler / Triads / Nodal)
- `Solution.AddElementalEulerXYAngle()` / `AddElementalEulerXZAngle()` / `AddElementalEulerYZAngle()` / `AddElementalTriads()`
- `Solution.AddNodalEulerXYAngle()` / `AddNodalEulerXZAngle()` / `AddNodalEulerYZAngle()` / `AddNodalTriads()`
- `Solution.AddVolume()` / `AddVolumeProbe()`
- `Solution.AddFractureTool()`

### Add* (utility)
- `Solution.AddCommandSnippet()`
- `Solution.AddPythonCodeEventBased()` / `AddPythonResult()`
- `Solution.AddUserDefinedResult()`
- `Solution.AddVariableGraph()`
- `Solution.AddResultMesh()`
- `Solution.AddComment()` / `AddFigure()` / `AddImage(string)`

### Properties
- `Solution.SolutionInformation`
- `Solution.TabularData`
- `Solution.CellId`
- `Solution.WorkingDir`
- `Solution.Status` ✓ verified (string-contains check `'Done' in str(...)`)
- `Solution.IsAutoHybridParallel`
- `Solution.ResultFilePath`
- `Solution.NumberOfDOF`
- `Solution.SparseMode`
- `Solution.SkipSolveCommand`
- `Solution.CyclicSectorDisplayRangeBegin`
- `Solution.NumberofSectors`
- `Solution.ElapsedRunTime`
- `Solution.MaximumRefinementLoops`
- `Solution.MemoryUsed`
- `Solution.RefinementDepth`
- `Solution.ResultFileDirectory`
- `Solution.ResultFileName`
- `Solution.ResultFileSize`
- `Solution.ResultFileTimestamp`
- `Solution.ExportTopologyFile`
- `Solution.MeshSource`
- `Solution.ElementSelection`
- `Solution.ResultFileUnitSystem`
- `Solution.CalculateBeamSectionResults`
- `Solution.TopologyResult`
- `Solution.DataModelObjectCategory`
- `Solution.Children`
- `Solution.Comments`
- `Solution.Figures`

---

## Ansys.ACT.Automation.Mechanical.Results.* (common result members)

These categories appear under `Ansys.ACT.Automation.Mechanical.Results.{DeformationResults | StressResults | StrainResults | ...}`. Common members across results:

- `result.Location` (settable to Selection or NamedSelection) ✓ verified
- `result.Mode = int` (modal) ✓ verified
- `result.Frequency` → Quantity (modal) ✓ verified
- `result.Maximum` → Quantity ✓ verified
- `result.Minimum` → Quantity
- `result.MaximumOfMaximumOverTime` → Quantity (transient) ✓ verified
- `result.Name`
- `result.Delete()` ✓ verified

### Specific classes
- `Results.DeformationResults.TotalDeformation` ✓ verified
- `Results.DeformationResults.DirectionalDeformation`
- `Results.StressResults.EquivalentStress` ✓ verified
- `Results.StressResults.MaximumPrincipalStress` / `MinimumPrincipalStress` / `MiddlePrincipalStress`
- `Results.StressResults.MaximumShearStress` / `NormalStress` / `ShearStress` / `StressIntensity`
- `Results.StrainResults.EquivalentElasticStrain` ✓ verified / `EquivalentPlasticStrain` / `MaximumPrincipalElasticStrain` / `ShearElasticStrain` / `NormalElasticStrain`

---

## Enums

Note: The `Ansys.ACT.WB1.xml` reference declares enum **type** signatures only via parameter types; the enum **field** values are not documented in this XML. Values below are canonical members.

### AnalysisType (`Ansys.Mechanical.DataModel.Enums.AnalysisType`)
- `AnalysisType.Static`
- `AnalysisType.Transient`
- `AnalysisType.Modal`
- `AnalysisType.Harmonic`
- `AnalysisType.Spectrum`
- `AnalysisType.Buckling`
- `AnalysisType.Shape`
- `AnalysisType.SubStructure`
- `AnalysisType.Steady` (steady-state thermal)
- `AnalysisType.ExplicitDynamics`

### LoadDefineBy (`Ansys.Mechanical.DataModel.Enums.LoadDefineBy`)
- `LoadDefineBy.Components` ✓ verified
- `LoadDefineBy.Vector`
- `LoadDefineBy.NormalToOrTangential`

### GeometryDefineByType (`Ansys.Mechanical.DataModel.Enums.GeometryDefineByType`)
- `GeometryDefineByType.GeometryEntities` ✓ verified
- `GeometryDefineByType.NamedSelection`
- `GeometryDefineByType.Worksheet`

### ContactType (`Ansys.Mechanical.DataModel.Enums.ContactType`)
- `ContactType.Bonded` ✓ verified
- `ContactType.NoSeparation`
- `ContactType.Frictionless`
- `ContactType.Rough`
- `ContactType.Frictional`
- `ContactType.ForcedFrictionalSliding`

### ContactFormulation (`Ansys.Mechanical.DataModel.Enums.ContactFormulation`)
- `ContactFormulation.AugmentedLagrange`
- `ContactFormulation.PurePenalty`
- `ContactFormulation.MPC`
- `ContactFormulation.Normal` / `NormalLagrange`
- `ContactFormulation.Beam`

### ContactBehavior (`Ansys.Mechanical.DataModel.Enums.ContactBehavior`)
- `ContactBehavior.ProgramControlled`
- `ContactBehavior.Symmetric`
- `ContactBehavior.Asymmetric`
- `ContactBehavior.AutoAsymmetric`

### AutomaticTimeStepping (`Ansys.Mechanical.DataModel.Enums.AutomaticTimeStepping`)
- `AutomaticTimeStepping.ProgramControlled`
- `AutomaticTimeStepping.On`
- `AutomaticTimeStepping.Off`

### TimeStepDefineByType (`Ansys.Mechanical.DataModel.Enums.TimeStepDefineByType`)
- `TimeStepDefineByType.Time`
- `TimeStepDefineByType.Substeps`

### DataModelObjectCategory (`Ansys.Mechanical.DataModel.Enums.DataModelObjectCategory`)
Members used in project (✓ verified): `Body`, `NamedSelection`, `Analysis`, `Solution`, `ContactRegion`.

Broader documented set:
- `Body`, `Part`, `NamedSelection`
- `Analysis`, `AnalysisSettings`, `Solution`, `SolutionInformation`
- `ContactRegion`, `Connections`, `Joint`, `Spring`, `Beam`
- `Material`, `MaterialAssignment`
- `Mesh`, `MeshConnection`, `Sizing`, `Refinement`, `Inflation`, `AutomaticMethod`, `FaceMeshing`
- `CoordinateSystem`, `RemotePoint`
- `Force`, `Pressure`, `Displacement`, `RemoteForce`, `Moment`
- `FixedSupport`, `FrictionlessSupport`, `CylindricalSupport`
- `Acceleration`, `StandardEarthGravity`, `RotationalVelocity`
- `Temperature`, `Convection`, `HeatFlow`, `HeatFlux`, `Radiation`
- `TotalDeformation`, `DirectionalDeformation`, `EquivalentStress`, `MaximumPrincipalStress`, `EquivalentElasticStrain`
- `UserDefinedResult`, `CommandSnippet`, `Comment`, `Figure`, `Image`
