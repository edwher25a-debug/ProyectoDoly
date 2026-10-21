using System;
using ProyectoDoly.Puentes.Ejes;

namespace ProyectoDoly.Puentes.Subestructura
{
    //Como se coloca la pila o el estribo respecto al placement
    public sealed class OpcionesUbicacion
    {
        //Desplazamiento en el plano del placement: + derecha del eje, + arriba (m)
        public double DesplazamientoLateral { get; set; }
        public double DesplazamientoVertical { get; set; }

        //Giro adicional en planta (grados, + antihorario), sumado al giro del placement
        public double GiroAdicional { get; set; }

        //true: la familia queda vertical; false: se inclina con la pendiente de la rasante
        public bool SiempreVertical { get; set; } = true;
    }

    //Punto de insercion y ejes locales (X = sentido del eje en el placement, Y = izquierda, Z = arriba), en coordenadas del eje (m)
    public readonly struct Ubicacion3
    {
        public Ubicacion3(Punto3 origen, Punto3 ejeX, Punto3 ejeY, Punto3 ejeZ)
        {
            Origen = origen;
            EjeX = ejeX;
            EjeY = ejeY;
            EjeZ = ejeZ;
        }

        public Punto3 Origen { get; }
        public Punto3 EjeX { get; }
        public Punto3 EjeY { get; }
        public Punto3 EjeZ { get; }
    }

    /// <summary>
    ///     Ubica una familia de subestructura en un placement del eje, como SOFiSTiK: el punto adaptativo (o el origen)
    ///     va al punto del eje en esa estacion, desplazado en el plano del placement, y la familia se orienta con el eje
    ///     girado por el placement.
    /// </summary>
    public static class UbicacionSubestructura
    {
        public static Ubicacion3 Calcular(Eje eje, Placement placement, OpcionesUbicacion opciones)
        {
            PuntoEje p = eje.Evaluar(placement.Estacion);
            double giro = (placement.Rotacion + opciones.GiroAdicional) * Math.PI / 180;

            //X: sentido del eje en planta, girado; Y: izquierda en el plano del placement
            double rumbo = p.Rumbo + giro;
            Punto3 x = new Punto3(Math.Cos(rumbo), Math.Sin(rumbo), 0);
            Punto3 y = new Punto3(-Math.Sin(rumbo), Math.Cos(rumbo), 0);
            Punto3 z = new Punto3(0, 0, 1);

            if (!opciones.SiempreVertical)
            {
                //Se inclina con la pendiente: X sube con la rasante y Z queda perpendicular a X
                double largo = Math.Sqrt(1 + p.Pendiente * p.Pendiente);
                x = new Punto3(Math.Cos(rumbo) / largo, Math.Sin(rumbo) / largo, p.Pendiente / largo);
                z = new Punto3(-Math.Cos(rumbo) * p.Pendiente / largo, -Math.Sin(rumbo) * p.Pendiente / largo, 1 / largo);
            }

            //Desplazamientos en el plano del placement (no en la perpendicular al eje cuando el apoyo esta esviado)
            Punto2 plano = PlacementsEje.DireccionPlano(eje, new Placement { Estacion = placement.Estacion, Rotacion = placement.Rotacion + opciones.GiroAdicional });
            Punto3 origen = new Punto3(
                p.Posicion.X - plano.X * opciones.DesplazamientoLateral,
                p.Posicion.Y - plano.Y * opciones.DesplazamientoLateral,
                p.Posicion.Z + opciones.DesplazamientoVertical);

            return new Ubicacion3(origen, x, y, z);
        }

        /// <summary>
        ///     Altura de la pila desde la cota de fundacion hasta el punto de insercion (m); nunca negativa
        /// </summary>
        public static double Altura(Ubicacion3 ubicacion, double cotaFundacion) => Math.Max(0, ubicacion.Origen.Z - cotaFundacion);
    }
}
