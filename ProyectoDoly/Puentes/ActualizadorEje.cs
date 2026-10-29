using Autodesk.Revit.DB;
using ProyectoDoly.Puentes.Dependencias;
using ProyectoDoly.Puentes.Ejes;
using ProyectoDoly.Puentes.Superestructura;

namespace ProyectoDoly.Puentes
{
    public sealed class ResultadoActualizacion
    {
        public int Ejes { get; set; }
        public int Tableros { get; set; }
        public int Pilas { get; set; }
        public int Cuantificados { get; set; }
        public List<string> Avisos { get; } = new List<string>();

        public bool Nada => Ejes + Tableros + Pilas == 0;

        public string Texto =>
            (Nada
                ? "No hay elementos atados a este eje para actualizar."
                : "Se actualizó lo que depende del eje:\n" +
                  (Ejes > 0 ? $"• {Ejes} ejes secundarios\n" : "") +
                  (Tableros > 0 ? $"• {Tableros} tableros y componentes barridos (vueltos a crear)\n" : "") +
                  (Pilas > 0 ? $"• {Pilas} pilas, estribos y cimentaciones (movidos a su placement)\n" : "") +
                  (Cuantificados > 0 ? $"• {Cuantificados} elementos vueltos a cuantificar\n" : "")) +
            "\nLas secciones y las rejillas no se mueven: vuelva a crearlas si hace falta. Los elementos creados antes de esta versión no están atados al eje." +
            (Avisos.Count > 0 ? "\n\nAvisos:\n" + string.Join("\n", Avisos.Distinct().Take(8)) + (Avisos.Count > 8 ? $"\n(y {Avisos.Count - 8} más)" : "") : "");
    }

    /// <summary>
    ///     Cuando un eje cambia (geometria, rasante o placements), actualiza lo que se creo sobre el: ejes secundarios
    ///     (se recalculan y actualizan a su vez lo suyo), pilas y estribos (se mueven a su placement sin borrarlos) y
    ///     tableros (se vuelven a crear con la misma receta). Al final se vuelve a cuantificar si el eje ya estaba cuantificado.
    ///     Debe llamarse sin transaccion abierta: los tableros adaptativos cargan su familia en el proyecto.
    /// </summary>
    public static class ActualizadorEje
    {
        public static ResultadoActualizacion Actualizar(Document doc, EjeGuardado eje)
        {
            ResultadoActualizacion resultado = new ResultadoActualizacion();
            Recorrer(doc, eje, resultado, new HashSet<ElementId>());
            return resultado;
        }

        private static void Recorrer(Document doc, EjeGuardado eje, ResultadoActualizacion resultado, HashSet<ElementId> vistos)
        {
            if (!vistos.Add(eje.Id) || !(doc.GetElement(eje.Id) is Element ejeElemento)) return;
            List<(Element elemento, Receta receta)> dependientes = DatosDependencia.Dependientes(doc, ejeElemento);

            //01_Ejes secundarios: se recalculan desde el principal y se recorren sus propios dependientes
            foreach ((Element elemento, Receta receta) in dependientes.Where(d => d.receta.Tipo == Recetas.EjeSecundario))
            {
                EjeGuardado? secundario = DatosEje.Leer(elemento);
                if (secundario == null) continue;
                try
                {
                    secundario.Eje = EjeSecundario.Crear(eje.Eje, Recetas.OpcionesEjeSecundario(receta));
                    secundario.Transformacion = eje.Transformacion;
                    using (Transaction transaccion = new Transaction(doc, $"Actualizar eje {secundario.Eje.Nombre}"))
                    {
                        transaccion.Start();
                        CreadorEje.Actualizar(doc, secundario);
                        transaccion.Commit();
                    }

                    resultado.Ejes++;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Autodesk.Revit.Exceptions.ApplicationException)
                {
                    resultado.Avisos.Add($"Eje secundario {elemento.Name}: {ex.Message}");
                    continue;
                }

                Recorrer(doc, secundario, resultado, vistos);
            }

            //02_Pilas y estribos: se mueven a su placement, sin borrarlos
            List<(Element elemento, Receta receta)> pilas = dependientes.Where(d => d.receta.Tipo == Recetas.Subestructura && d.elemento is FamilyInstance).ToList();
            if (pilas.Count > 0)
                using (Transaction transaccion = new Transaction(doc, $"Mover subestructura {eje.Eje.Nombre}"))
                {
                    transaccion.Start();
                    foreach ((Element elemento, Receta receta) in pilas)
                    {
                        try
                        {
                            string aviso = CreadorSubestructura.Reubicar(doc, (FamilyInstance)elemento, eje, receta);
                            if (aviso.Length > 0) resultado.Avisos.Add(aviso);
                            DatosDependencia.Guardar(elemento, ejeElemento, receta);
                            resultado.Pilas++;
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Autodesk.Revit.Exceptions.ApplicationException)
                        {
                            resultado.Avisos.Add($"{elemento.Name} ({elemento.Id}): {ex.Message}");
                        }
                    }

                    transaccion.Commit();
                }

            //03_Tableros: uno por clave de grupo (todos los tramos de un mismo tablero). Primero se crea el nuevo y,
            //si sale bien, se borra el anterior
            foreach (IGrouping<string, (Element elemento, Receta receta)> tablero in dependientes
                         .Where(d => d.receta.Tipo == Recetas.Superestructura).GroupBy(d => d.receta.Texto("grupo")))
            {
                Receta receta = tablero.First().receta;
                string nombre = $"{receta.Texto("componente", "Tablero")} {eje.Eje.Nombre} {receta.Texto("nombre")}".Trim();
                try
                {
                    OpcionesSuperestructura opciones = OpcionesSuperestructura.DesdeReceta(receta, eje);
                    if (opciones.Variables.Count > 0)
                    {
                        if (!(doc.GetElement(opciones.TipoSeccion) is FamilySymbol tipo))
                            throw new InvalidOperationException("ya no está en el modelo la familia de sección con sus parámetros variables.");
                        using (LectorSeccion lector = new LectorSeccion(doc, tipo))
                            opciones.PorEstacion = SeccionesPorEstacion.Calcular(lector, opciones.Estaciones(), opciones.Variables, opciones.EspejoVariables);
                    }

                    if (opciones.FamiliaAdaptativa) FamiliaTablero.Crear(doc, opciones);
                    else CreadorSuperestructura.Crear(doc, opciones);

                    using (Transaction transaccion = new Transaction(doc, $"Quitar {nombre} anterior"))
                    {
                        transaccion.Start();
                        Borrar(doc, tablero.Select(t => t.elemento).ToList());
                        transaccion.Commit();
                    }

                    resultado.Tableros++;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Autodesk.Revit.Exceptions.ApplicationException)
                {
                    resultado.Avisos.Add($"{nombre}: no se pudo volver a crear ({ex.Message}). Quedó el anterior.");
                }
            }

            //04_Si el eje ya estaba cuantificado, se vuelve a cuantificar con las estaciones nuevas
            List<ElementId> creados = CuantificadorPuente.CreadosEnEje(doc, eje.Eje);
            bool cuantificado = creados.Any(id => !string.IsNullOrEmpty(doc.GetElement(id)?.get_Parameter(ParametrosPuente.Eje.Guid)?.AsString()));
            if (cuantificado && resultado.Tableros + resultado.Pilas > 0)
                try
                {
                    CuantificadorPuente.Cuantificar(doc, new OpcionesCuantificacion { Eje = eje, Elementos = creados });
                    resultado.Cuantificados += creados.Count;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is Autodesk.Revit.Exceptions.ApplicationException)
                {
                    resultado.Avisos.Add($"Cuantificar {eje.Eje.Nombre}: {ex.Message}");
                }
        }

        //Los tramos adaptativos estan agrupados: se borra el tipo de grupo (con su grupo y miembros); lo suelto, uno a uno
        private static void Borrar(Document doc, List<Element> elementos)
        {
            HashSet<ElementId> tiposGrupo = new HashSet<ElementId>();
            List<ElementId> sueltos = new List<ElementId>();
            foreach (Element e in elementos)
            {
                if (e.GroupId != ElementId.InvalidElementId && doc.GetElement(e.GroupId) is Group grupo) tiposGrupo.Add(grupo.GetTypeId());
                else sueltos.Add(e.Id);
            }

            if (tiposGrupo.Count > 0) doc.Delete(tiposGrupo.ToList());
            List<ElementId> quedan = sueltos.Where(id => doc.GetElement(id) != null).ToList();
            if (quedan.Count > 0) doc.Delete(quedan);
        }
    }
}
