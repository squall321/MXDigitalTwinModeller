namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simulation
{
    public class SimulationParameters
    {
        // General
        public string Title { get; set; }
        public SimulationType Type { get; set; }

        // ── Eigenvalue Settings ──
        public int NumModes { get; set; }               // NEIG
        public double MinFrequency { get; set; }        // Hz, 0=no lower limit
        public double MaxFrequency { get; set; }        // Hz, 0=no upper limit
        public int EigenvalueMethod { get; set; }       // EIGMTH (2=Lanczos, 3=InversePower, 101=BCSLIB)

        // ── Solver Settings ──
        public int SolverType { get; set; }             // LSOLVR
        public bool AutoSPC { get; set; }               // AUTOSPC (자동 단점 구속)
        public int NegativeEigenvalue { get; set; }     // NEGEV (0=stop, 1=warn, 2=allow)

        // ── Implicit General ──
        public bool GeometricStiffness { get; set; }    // IGS (기하 강성)
        public int ImplicitFormulation { get; set; }    // IMFORM (2=eigenvalue, 12=static+eigen)

        // ── Output ──
        public bool OutputEigout { get; set; }          // DATABASE_EIGOUT
        public bool OutputD3plot { get; set; }          // DATABASE_BINARY_D3PLOT
        public bool OutputNodeout { get; set; }         // DATABASE_NODOUT
        public bool OutputElout { get; set; }           // DATABASE_ELOUT

        // ── Additional Controls ──
        public bool ControlEnergy { get; set; }
        public bool ControlHourglass { get; set; }
        public bool ControlAccuracy { get; set; }
        public int HourglassType { get; set; }          // IHQ
        public double HourglassCoeff { get; set; }      // QH

        public SimulationParameters()
        {
            Title = "Modal_Analysis";
            Type = SimulationType.ModalAnalysis;
            NumModes = 10;
            MinFrequency = 0;
            MaxFrequency = 0;
            EigenvalueMethod = 2;   // Lanczos
            SolverType = 2;         // Multi-frontal sparse
            AutoSPC = true;
            NegativeEigenvalue = 2; // Allow
            GeometricStiffness = false;
            ImplicitFormulation = 2;
            OutputEigout = true;
            OutputD3plot = true;
            OutputNodeout = false;
            OutputElout = false;
            ControlEnergy = true;
            ControlHourglass = true;
            ControlAccuracy = true;
            HourglassType = 6;
            HourglassCoeff = 0.1;
        }

        public SimulationParameters Clone()
        {
            return new SimulationParameters
            {
                Title = Title,
                Type = Type,
                NumModes = NumModes,
                MinFrequency = MinFrequency,
                MaxFrequency = MaxFrequency,
                EigenvalueMethod = EigenvalueMethod,
                SolverType = SolverType,
                AutoSPC = AutoSPC,
                NegativeEigenvalue = NegativeEigenvalue,
                GeometricStiffness = GeometricStiffness,
                ImplicitFormulation = ImplicitFormulation,
                OutputEigout = OutputEigout,
                OutputD3plot = OutputD3plot,
                OutputNodeout = OutputNodeout,
                OutputElout = OutputElout,
                ControlEnergy = ControlEnergy,
                ControlHourglass = ControlHourglass,
                ControlAccuracy = ControlAccuracy,
                HourglassType = HourglassType,
                HourglassCoeff = HourglassCoeff
            };
        }
    }
}
