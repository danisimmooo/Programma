using System;
using System.Drawing;
using System.Windows.Forms;

namespace HeatConductivityApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }

    public enum MaterialType { Concrete, Brick, Insulation, Foam, Steel, Air, Custom }

    public class Material
    {
        public string Name { get; set; }
        public float Conductivity { get; set; }
        public Color BaseColor { get; set; }
        public MaterialType Type { get; set; }

        public Material(string name, float conductivity, Color color, MaterialType type)
        {
            Name = name;
            Conductivity = conductivity;
            BaseColor = color;
            Type = type;
        }

        public override string ToString() => Name;
    }

    public class ThermalPhysics
    {
        public int Nx { get; }
        public int Ny { get; }
        public float CellSize { get; set; } = 0.01f;

        public float[,] Temperature { get; private set; }
        public Material[,] GridMaterials { get; private set; }

        public float TempInside { get; set; } = 20.0f;
        public float TempOutside { get; set; } = -24.0f;

        public ThermalPhysics(int nx, int ny)
        {
            Nx = nx;
            Ny = ny;
            Temperature = new float[nx, ny];
            GridMaterials = new Material[nx, ny];
        }

        public void InitializeGrid(Material defaultMaterial)
        {
            for (int x = 0; x < Nx; x++)
            {
                for (int y = 0; y < Ny; y++)
                {
                    GridMaterials[x, y] = defaultMaterial;
                    Temperature[x, y] = TempInside;
                }
            }
        }

        public void SetMaterialRegion(int startX, int startY, int width, int height, Material mat)
        {
            for (int x = startX; x < Math.Min(startX + width, Nx); x++)
            {
                for (int y = startY; y < Math.Min(startY + height, Ny); y++)
                {
                    if (x >= 0 && y >= 0)
                        GridMaterials[x, y] = mat;
                }
            }
        }

        public void StepSimulation(int steps = 10)
        {
            float[,] nextTemp = (float[,])Temperature.Clone();

            for (int iter = 0; iter < steps; iter++)
            {
                for (int x = 1; x < Nx - 1; x++)
                {
                    for (int y = 1; y < Ny - 1; y++)
                    {
                        float k = GridMaterials[x, y].Conductivity;
                        float kRight = GridMaterials[x + 1, y].Conductivity;
                        float kLeft = GridMaterials[x - 1, y].Conductivity;
                        float kUp = GridMaterials[x, y + 1].Conductivity;
                        float kDown = GridMaterials[x, y - 1].Conductivity;

                        float kR = 2 * k * kRight / (k + kRight + 1e-6f);
                        float kL = 2 * k * kLeft / (k + kLeft + 1e-6f);
                        float kU = 2 * k * kUp / (k + kUp + 1e-6f);
                        float kD = 2 * k * kDown / (k + kDown + 1e-6f);

                        float fluxX = kR * (Temperature[x + 1, y] - Temperature[x, y]) - kL * (Temperature[x, y] - Temperature[x - 1, y]);
                        float fluxY = kU * (Temperature[x, y + 1] - Temperature[x, y]) - kD * (Temperature[x, y] - Temperature[x, y - 1]);

                        nextTemp[x, y] = Temperature[x, y] + 0.15f * (fluxX + fluxY) / (k * 4.0f);
                    }
                }

                for (int y = 0; y < Ny; y++)
                {
                    nextTemp[0, y] = TempInside;
                    nextTemp[Nx - 1, y] = TempOutside;
                }

                for (int x = 0; x < Nx; x++)
                {
                    nextTemp[x, 0] = nextTemp[x, 1];
                    nextTemp[x, Ny - 1] = nextTemp[x, Ny - 2];
                }

                Array.Copy(nextTemp, Temperature, Temperature.Length);
            }
        }
    }
}