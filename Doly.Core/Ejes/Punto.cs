using System;

namespace Doly.Core.Ejes
{
    //Punto o vector en planta (X = Este, Y = Norte), en metros
    public readonly struct Punto2
    {
        public double X { get; }
        public double Y { get; }

        public Punto2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Longitud => Math.Sqrt(X * X + Y * Y);

        public static Punto2 operator +(Punto2 a, Punto2 b) => new Punto2(a.X + b.X, a.Y + b.Y);
        public static Punto2 operator -(Punto2 a, Punto2 b) => new Punto2(a.X - b.X, a.Y - b.Y);
        public static Punto2 operator *(Punto2 a, double f) => new Punto2(a.X * f, a.Y * f);

        //Vector unitario con el rumbo dado (radianes desde +X, antihorario)
        public static Punto2 Direccion(double rumbo) => new Punto2(Math.Cos(rumbo), Math.Sin(rumbo));

        public double Distancia(Punto2 otro) => (this - otro).Longitud;

        public override string ToString() => $"({X:F4}, {Y:F4})";
    }

    //Punto 3D en metros
    public readonly struct Punto3
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Punto3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
    }
}
