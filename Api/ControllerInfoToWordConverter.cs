using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentaConAI.Api
{
    public static class ControllerInfoToWordConverter
    {
        public static void ToWord(string fileName, Dictionary<string, List<ControllerInfo>> allControllers)
        {
            if (allControllers.Values.Any(list => list.Count > 0))
            {               
                using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(fileName, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    Body body = mainPart.Document.AppendChild(new Body());

                    // Create the table
                    Table table = new Table();
                    body.AppendChild(table);

                    // Add the header row
                    TableRow headerRow = new TableRow();
                    table.AppendChild(headerRow);

                    // Create header cells
                    TableCell headerCell1 = new TableCell(new Paragraph(new Run(new Text("Ruta del Endpoint"))));
                    TableCell headerCell2 = new TableCell(new Paragraph(new Run(new Text("Tipo de Método"))));
                    TableCell headerCell3 = new TableCell(new Paragraph(new Run(new Text("Request Object"))));
                    TableCell headerCell4 = new TableCell(new Paragraph(new Run(new Text("Response Object"))));
                    headerRow.Append(headerCell1);
                    headerRow.Append(headerCell2);
                    headerRow.Append(headerCell3);
                    headerRow.Append(headerCell4);

                    // Define cell borders
                    TableCellBorders cellBorders = new TableCellBorders(
                        new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" },
                        new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 24, Color = "000000" }
                    );

                    // Add rows for the controllers
                    foreach (var appEntry in allControllers)
                    {
                        foreach (var controller in appEntry.Value)
                        {
                            foreach (var endpoint in controller.Endpoints)
                            {
                                // Create a new row
                                TableRow row = new TableRow();
                                table.AppendChild(row);

                                // Create cells for the row
                                TableCell cell1 = CreateTableCell(endpoint.Route, "18");
                                TableCell cell2 = CreateTableCell(endpoint.HttpMethod, "18");
                                TableCell cell3 = CreateTableCell(endpoint.RequestObject, "18");
                                TableCell cell4 = CreateTableCell(endpoint.ResponseObject, "18");

                                cell1.Append(cellBorders.CloneNode(true));
                                cell2.Append(cellBorders.CloneNode(true));
                                cell3.Append(cellBorders.CloneNode(true));
                                cell4.Append(cellBorders.CloneNode(true));

                                row.Append(cell1);
                                row.Append(cell2);
                                row.Append(cell3);
                                row.Append(cell4);
                            }
                        }
                    }

                    // Save changes to the document
                    mainPart.Document.Save();
                }
            }
        }

        // Método auxiliar para crear celdas de la tabla
        private static TableCell CreateTableCell(string text, string fontSize)
        {
            var cell = new TableCell();
            var paragraph = new Paragraph();
            var run = new Run();
            var runProperties = new RunProperties(new FontSize { Val = fontSize });
            var textElement = new Text { Text = text };

            run.Append(runProperties);
            run.Append(textElement);
            paragraph.Append(run);
            cell.Append(paragraph);

            return cell;
        }

    }
}
