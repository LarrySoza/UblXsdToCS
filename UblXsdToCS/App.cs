//----------------------------------------------------------------------------
//    Copyleft (ɔ) 2020  Gaspersoft
//    Dudas o consultas escribir a it@gaspersoft.com
//----------------------------------------------------------------------------
using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace UblXsdToCS
{
    public class App
    {
        public static string rutaSalida = Path.GetFullPath("..\\SrcGenerado");

        static void Main(string[] args)
        {
            //public static string rootFolder = Path.GetFullPath("..\\Xsd1.0\\maindoc");
            //public static string includeFolder = Path.GetFullPath("..\\Xsd1.0\\common");
            //public static string rootFolder = Path.GetFullPath("..\\Xsd2.1\\maindoc");
            //public static string includeFolder = Path.GetFullPath("..\\Xsd2.1\\common");

            //Genera UBL 2.0
            Generar("..\\Xsd2.0\\maindoc", "..\\Xsd2.0\\common", "GasperSoft.Ubl", "Ubl.cs");
            Console.WriteLine();

            //Genera UBL 2.1
            Generar("..\\Xsd2.1\\maindoc", "..\\Xsd2.1\\common", "GasperSoft.Ubl.V2", "UblV2.cs");
            Console.WriteLine("Presione una tecla para continuar...");

            Console.ReadKey();
        }

        private static void Generar(string rootFolder, string includeFolder,string _namespace,string nombreArchivo)
        {
            //Aqui voy agregando todos los esquemas que voy encontrando
            var schemas = new XmlSchemas();
            var listRootSchemas = new List<XmlSchema>();

            //Aqui Almaceno todos los include
            var schemaSet = new List<XmlSchemaExternal>();

            //Comienso leyendo los xsd que se encuentran en el directorio de root
            var dir = new DirectoryInfo(rootFolder);

            //En este punto ya tengo todos los XSD que voy a procesar
            foreach (var item in dir.GetFiles("*.xsd"))
            {
                //leo el esquema y lo guardo
                var schema = GetSchemaFromFile(item.Name, rootFolder);
                listRootSchemas.Add(schema);

                //Aqui voy consultando los includes existentes y los guardo en schemaSet(recordar que esta variable que al ser una lista este se pasa como referencia si o si)
                ExtractIncludes(includeFolder, schema, schemaSet);
            }

            var includes = new StringBuilder();
            foreach (var elemento in schemaSet)
            {
                includes.AppendLine(elemento.SchemaLocation);
            }

            Console.WriteLine("Se encontraron las siguientes dependencias");
            Console.WriteLine(includes.ToString());

            //Voy juntando todos mis esquemas principales
            listRootSchemas.ForEach(elemento => schemas.Add(elemento));

            //Aqui schemaSet ya contiene toda las rutas de los includes necesarios entonces los leo y los agrego a mis schemas
            schemaSet.ForEach(schemaExternal => schemas.Add(GetSchemaFromFile(schemaExternal.SchemaLocation, includeFolder)));

            schemas.Compile(null, true);

            //Llegando a este puntos ya tenemos todos los esquemas cargados correctamente
            Console.WriteLine("Lectura de esquemas correcta");

            //Aqui procedemos a Generar el codigo mirar http://mikehadlow.blogspot.pe/2007/01/writing-your-own-xsdexe.html
            //Tambien leer aqui https://weblogs.asp.net/cazzu/33302


            var xmlSchemaImporter = new XmlSchemaImporter(schemas);
            //var codeNamespace = new CodeNamespace("SimpleUbl.Xml");
            var codeNamespace = new CodeNamespace(_namespace);

            var xmlCodeExporter = new XmlCodeExporter(codeNamespace);

            var xmlTypeMappings = new List<XmlTypeMapping>();

            //Voy a recorrer todos los esquemas de rootFolder
            foreach (var xsd in listRootSchemas)
            {
                //foreach (XmlSchemaType schemaType in xsd.SchemaTypes.Values)
                //    xmlTypeMappings.Add(xmlSchemaImporter.ImportSchemaType(schemaType.QualifiedName));

                //foreach (XmlSchemaElement schemaElement in xsd.Elements.Values)
                //    xmlTypeMappings.Add(xmlSchemaImporter.ImportTypeMapping(schemaElement.QualifiedName));

                foreach (XmlSchemaObject item in xsd.Items)
                {
                    if (item is XmlSchemaElement)
                    {
                        var type = xmlSchemaImporter.ImportTypeMapping(new System.Xml.XmlQualifiedName(((XmlSchemaElement)item).Name, xsd.TargetNamespace));
                        xmlTypeMappings.Add(type);
                        Console.WriteLine($"Se encontro : {type.TypeName}");
                    }
                }
            }

            //Aqui Agrego todos los elementos a mi XmlCodeExporter
            xmlTypeMappings.ForEach(xmlCodeExporter.ExportTypeMapping);


            CodeGenerator.ValidateIdentifiers(codeNamespace);

            //Aqui solo por moneria le pongo que fue generado por la utilidad UblXsd :P y arreglo un poco el codigo
            GeneratedCodeAttribute generatedCodeAttribute = new GeneratedCodeAttribute("ITUblXsd", "1.0.0.0");
            CodeAttributeDeclaration codeAttrDecl =
            new CodeAttributeDeclaration("System.CodeDom.Compiler.GeneratedCodeAttribute",
                new CodeAttributeArgument(
                    new CodePrimitiveExpression(generatedCodeAttribute.Tool)),
                new CodeAttributeArgument(
                    new CodePrimitiveExpression(generatedCodeAttribute.Version)));

            foreach (CodeTypeDeclaration codeTypeDeclaration in codeNamespace.Types)
            {
                for (int i = codeTypeDeclaration.CustomAttributes.Count - 1; i >= 0; i--)
                {
                    CodeAttributeDeclaration cad = codeTypeDeclaration.CustomAttributes[i];
                    if (cad.Name == "System.CodeDom.Compiler.GeneratedCodeAttribute")
                    {
                        //codeTypeDeclaration.CustomAttributes.RemoveAt(i);
                        codeTypeDeclaration.CustomAttributes[i] = codeAttrDecl;
                    }

                    if (cad.Name == "System.Diagnostics.DebuggerStepThroughAttribute")
                        codeTypeDeclaration.CustomAttributes.RemoveAt(i);
                    if (cad.Name == "System.ComponentModel.DesignerCategoryAttribute")
                        codeTypeDeclaration.CustomAttributes.RemoveAt(i);
                }
            }

            using (var writer = new StringWriter())
            {
                writer.WriteLine("//----------------------------------------------------------------------------");
                writer.WriteLine("// <auto-generated>");
                writer.WriteLine($"//    Generado por UblXsdToCS el: {DateTime.Now.ToString()}");
                writer.WriteLine("//    *** No es recomendable editar este archivo ***");
                writer.WriteLine("//    Actualizaciones en https://github.com/LarrySoza/UblXsdToCS");
                writer.WriteLine("//    Dudas o consultas escribir a it@gaspersoft.com");
                writer.WriteLine("// </auto-generated>");
                writer.WriteLine("//----------------------------------------------------------------------------");
                writer.WriteLine();
                writer.WriteLine("using System;");
                writer.WriteLine("using System.Xml;");
                writer.WriteLine("using System.Xml.Serialization;");
                writer.WriteLine("using System.CodeDom.Compiler;");
                writer.WriteLine();

                string src = string.Empty;

                using (var sourceWriter = new StringWriter())
                {
                    var options = new CodeGeneratorOptions();
                    options.ElseOnClosing = true;
                    options.BracingStyle = "C";
                    options.BlankLinesBetweenMembers = false;

                    //CompilerParameters opt = new CompilerParameters(new string[]{
                    //                  "System.dll",
                    //                  "System.Xml.dll",
                    //                  "System.Windows.Forms.dll",
                    //                  "System.Data.dll",
                    //                  "System.Drawing.dll"});

                    var provider = new CSharpCodeProvider();

                    provider.GenerateCodeFromNamespace(codeNamespace, sourceWriter, options);

                    var source = sourceWriter.GetStringBuilder().Replace("/// <comentarios/>", "");
                    source = source.Replace("System.Xml.Serialization.", "");
                    source = source.Replace("System.CodeDom.Compiler.", "");
                    source = source.Replace("System.Xml.", "");
                    source = source.Replace("System.", "");
                    source = source.Replace("GeneratedCodeAttribute", "GeneratedCode");
                    source = source.Replace("SerializableAttribute", "Serializable");
                    source = source.Replace("XmlTypeAttribute", "XmlType");
                    source = source.Replace("XmlRootAttribute", "XmlRoot");
                    source = source.Replace("XmlIncludeAttribute", "XmlInclude");
                    source = source.Replace("XmlAttributeAttribute", "XmlAttribute");
                    source = source.Replace("XmlEnumAttribute", "XmlEnum");
                    source = source.Replace("XmlElementAttribute", "XmlElement");
                    source = source.Replace("XmlTextAttribute", "XmlText");
                    source = source.Replace("XmlArrayAttribute", "XmlArray");
                    source = source.Replace("XmlArrayItemAttribute", "XmlArrayItem");
                    source = source.Replace("XmlIgnoreAttribute", "XmlIgnore");

                    //Por defecto se genera codigo con "public partial" yo quiero que todos mis metodos sean internal
                    source = source.Replace("public partial", "public");

                    src = source.ToString();
                }

                writer.Write(src);

                File.WriteAllText(Path.Combine(rutaSalida, nombreArchivo), writer.ToString());
            }

            Console.WriteLine(String.Format("Archivo generador Correctamente en  {0}", Path.Combine(rutaSalida, nombreArchivo)));
        }

        private static XmlSchema GetSchemaFromFile(string fileName, string directorio)
        {
            using (var fs = new FileStream(Path.Combine(directorio, fileName), FileMode.Open))
            {
                return XmlSchema.Read(fs, null);
            }
        }

        private static void ExtractIncludes(string includeFolder,XmlSchema xmlSchema, List<XmlSchemaExternal> schemaList)
        {
            foreach (XmlSchemaExternal include in xmlSchema.Includes)
            {
                var SchemaLocation = new DirectoryInfo(Path.Combine(includeFolder, include.SchemaLocation)).FullName;
                include.SchemaLocation = SchemaLocation;

                if (!schemaList.Select(s =>  s.SchemaLocation).Contains(include.SchemaLocation))
                    schemaList.Add(include);

                if (include.Schema == null)
                {
                    XmlSchema schema = GetSchemaFromFile(include.SchemaLocation, includeFolder);
                    ExtractIncludes(includeFolder, schema, schemaList);
                }
                else
                    ExtractIncludes(includeFolder, include.Schema, schemaList);
            }
        }
    }
}
