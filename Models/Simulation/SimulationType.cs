namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simulation
{
    public enum SimulationType
    {
        ModalAnalysis           // 구조 모드 해석 (Structural Modal Analysis)
    }

    public enum EigenvalueMethod
    {
        Lanczos = 2,            // Block Shift and Invert Lanczos (기본, 권장)
        InversePower = 3,       // Inverse Power Method
        BCSLIB = 101            // BCSLIB-EXT
    }

    public enum ImplicitSolver
    {
        MultiFrontal = 2,       // Multi-frontal Sparse (기본)
        PARDISO = 4,            // Intel PARDISO
        MUMPS = 6               // MUMPS Distributed
    }
}
