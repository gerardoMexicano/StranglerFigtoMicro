Imports System.IO
Imports System.IO.Compression
Imports System.Xml.Linq
Imports System.Linq
Imports iTextSharp.text
Imports iTextSharp.text.pdf

Public Class Reportes
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
    End Sub

    Protected Sub btnGenerarPDFs_Click(sender As Object, e As EventArgs)
        If Not fuXmlFiles.HasFiles Then
            litStatus.Text = "Por favor, selecciona al menos un archivo XML."
            Return
        End If

        Dim basePath As String = Server.MapPath("~")
        
        ' Carpeta temporal para los XMLs subidos
        Dim tempXmlDir As String = Path.Combine(basePath, "temporal_xmls")
        If Directory.Exists(tempXmlDir) Then Directory.Delete(tempXmlDir, True)
        Directory.CreateDirectory(tempXmlDir)

        Dim xmlFiles As New List(Of String)()

        For Each uploadedFile In fuXmlFiles.PostedFiles
            If Path.GetExtension(uploadedFile.FileName).ToLower() = ".xml" Then
                Dim savePath As String = Path.Combine(tempXmlDir, Path.GetFileName(uploadedFile.FileName))
                uploadedFile.SaveAs(savePath)
                xmlFiles.Add(savePath)
            End If
        Next

        If xmlFiles.Count = 0 Then
            litStatus.Text = "No se subieron archivos XML válidos."
            Return
        End If

        ' Carpeta temporal para los PDFs
        Dim tempDir As String = Path.Combine(basePath, "temporal_pdfs")
        If Directory.Exists(tempDir) Then Directory.Delete(tempDir, True)
        Directory.CreateDirectory(tempDir)

        Dim facturas = ProcesarFacturas(xmlFiles)
        
        Dim i As Integer = 1
        For Each fac In facturas
            If fac.EsXMLValido Then
                Dim pdfPath As String = Path.Combine(tempDir, "Reporte_" & fac.Timbre?.UUID & ".pdf")
                GenerarPdfBasic(fac, pdfPath)
            End If
            i += 1
        Next

        ' Comprimir en ZIP
        Dim zipPath As String = Path.Combine(basePath, "ReportesGenerados.zip")
        If File.Exists(zipPath) Then File.Delete(zipPath)
        
        ZipFile.CreateFromDirectory(tempDir, zipPath)
        
        ' Limpiar temporales
        Directory.Delete(tempDir, True)
        Directory.Delete(tempXmlDir, True)

        litStatus.Text = String.Format("Se generaron {0} PDFs de {1} XMLs. <a href='ReportesGenerados.zip' download>Descargar ZIP</a>",
            facturas.Where(Function(f) f.EsXMLValido).Count(),
            xmlFiles.Count)
    End Sub

    Private Sub GenerarPdfBasic(fac As Cfdi40, pdfPath As String)
        Using fs As New FileStream(pdfPath, FileMode.Create, FileAccess.Write, FileShare.None)
            Dim doc As New Document(PageSize.A4, 36, 36, 36, 36)
            Dim writer As PdfWriter = PdfWriter.GetInstance(doc, fs)
            doc.Open()
            
            Dim fontTitle As New Font(Font.FontFamily.HELVETICA, 16, Font.BOLD)
            Dim fontHeader As New Font(Font.FontFamily.HELVETICA, 12, Font.BOLD)
            Dim fontNormal As New Font(Font.FontFamily.HELVETICA, 10, Font.NORMAL)
            
            doc.Add(New Paragraph("Reporte CFDI " & fac.Version, fontTitle))
            doc.Add(New Paragraph("UUID: " & fac.Timbre?.UUID, fontNormal))
            doc.Add(New Paragraph("Folio: " & fac.Folio & " | Serie: " & fac.Serie, fontNormal))
            doc.Add(New Paragraph("Fecha: " & fac.Fecha, fontNormal))
            doc.Add(New Paragraph(" "))
            
            ' Emisor
            If fac.Emisor IsNot Nothing Then
                doc.Add(New Paragraph("[Nodo Emisor]", fontHeader))
                doc.Add(New Paragraph("RFC: " & fac.Emisor.Rfc, fontNormal))
                doc.Add(New Paragraph("Nombre: " & fac.Emisor.Nombre, fontNormal))
                doc.Add(New Paragraph("Régimen: " & fac.Emisor.RegimenFiscal, fontNormal))
                doc.Add(New Paragraph(" "))
            End If
            
            ' Receptor
            If fac.Receptor IsNot Nothing Then
                doc.Add(New Paragraph("[Nodo Receptor]", fontHeader))
                doc.Add(New Paragraph("RFC: " & fac.Receptor.Rfc, fontNormal))
                doc.Add(New Paragraph("Nombre: " & fac.Receptor.Nombre, fontNormal))
                doc.Add(New Paragraph("Uso CFDI: " & fac.Receptor.UsoCFDI, fontNormal))
                doc.Add(New Paragraph(" "))
            End If
            
            ' Conceptos
            If fac.Conceptos IsNot Nothing AndAlso fac.Conceptos.Count > 0 Then
                doc.Add(New Paragraph("[Nodo Conceptos]", fontHeader))
                For Each c In fac.Conceptos
                    doc.Add(New Paragraph("- Clave: " & c.ClaveProdServ & " | Cantidad: " & c.Cantidad & " | Desc: " & c.Descripcion, fontNormal))
                    doc.Add(New Paragraph("  Valor Unitario: " & c.ValorUnitario & " | Importe: " & c.Importe, fontNormal))
                Next
                doc.Add(New Paragraph(" "))
            End If
            
            ' Totales
            doc.Add(New Paragraph("[Totales]", fontHeader))
            doc.Add(New Paragraph("SubTotal: " & fac.Subtotal, fontNormal))
            doc.Add(New Paragraph("Total: " & fac.Total, fontNormal))
            doc.Add(New Paragraph("Moneda: " & fac.Moneda, fontNormal))
            
            doc.Close()
        End Using
    End Sub

    ' ─────────────────────────────────────────────
    ' CÓDIGO DE LECTOR XML PEGADO DIRECTAMENTE AQUÍ
    ' ─────────────────────────────────────────────
    Public ReadOnly ns4 As XNamespace = "http://www.sat.gob.mx/cfd/4"
    Public ReadOnly tfd As XNamespace = "http://www.sat.gob.mx/TimbreFiscalDigital"

    Public Class Cfdi40
        Public Property EsXMLValido As Boolean
        Public Property Version As String
        Public Property Serie As String
        Public Property Folio As String
        Public Property Fecha As String
        Public Property Subtotal As String
        Public Property Descuento As String
        Public Property Total As String
        Public Property Moneda As String
        Public Property TipoCambio As String
        Public Property TipoDeComprobante As String
        Public Property FormaDePago As String
        Public Property MetodoDePago As String
        Public Property LugarExpedicion As String
        Public Property Condiciones As String
        Public Property Sello As String
        Public Property NoCertificado As String
        Public Property Certificado As String

        Public Property Emisor As EmisorCfdi
        Public Property Receptor As ReceptorCfdi
        Public Property Conceptos As List(Of ConceptoCfdi)
        Public Property Timbre As TimbreFiscal
    End Class

    Public Class EmisorCfdi
        Public Property Rfc As String
        Public Property Nombre As String
        Public Property RegimenFiscal As String
    End Class

    Public Class ReceptorCfdi
        Public Property Rfc As String
        Public Property Nombre As String
        Public Property UsoCFDI As String
        Public Property DomicilioFiscalReceptor As String
        Public Property RegimenFiscalReceptor As String
    End Class

    Public Class ConceptoCfdi
        Public Property ClaveProdServ As String
        Public Property Cantidad As String
        Public Property ClaveUnidad As String
        Public Property Unidad As String
        Public Property Descripcion As String
        Public Property ValorUnitario As String
        Public Property Importe As String
        Public Property Descuento As String
        Public Property ObjetoImp As String
    End Class

    Public Class TimbreFiscal
        Public Property UUID As String
        Public Property FechaTimbrado As String
        Public Property RfcProvCertif As String
        Public Property NoCertificadoSAT As String
        Public Property SelloCFD As String
        Public Property SelloSAT As String
    End Class

    Public Function ReadComprobante40(xDoc As XDocument) As Cfdi40
        Try
            Dim comp = xDoc.Descendants(ns4 + "Comprobante").FirstOrDefault()
            If comp Is Nothing Then Return Nothing

            Dim fac As New Cfdi40 With {
                .EsXMLValido    = True,
                .Version        = If(comp.Attributes("Version").Any(), comp.Attribute("Version").Value, String.Empty),
                .Serie          = If(comp.Attributes("Serie").Any(), comp.Attribute("Serie").Value, String.Empty),
                .Folio          = If(comp.Attributes("Folio").Any(), comp.Attribute("Folio").Value, String.Empty),
                .Fecha          = If(comp.Attributes("Fecha").Any(), comp.Attribute("Fecha").Value, String.Empty),
                .Subtotal       = If(comp.Attributes("SubTotal").Any(), comp.Attribute("SubTotal").Value, String.Empty),
                .Descuento      = If(comp.Attributes("Descuento").Any(), comp.Attribute("Descuento").Value, String.Empty),
                .Total          = If(comp.Attributes("Total").Any(), comp.Attribute("Total").Value, String.Empty),
                .Moneda         = If(comp.Attributes("Moneda").Any(), comp.Attribute("Moneda").Value, String.Empty),
                .TipoCambio     = If(comp.Attributes("TipoCambio").Any(), comp.Attribute("TipoCambio").Value, String.Empty),
                .TipoDeComprobante = If(comp.Attributes("TipoDeComprobante").Any(), comp.Attribute("TipoDeComprobante").Value, String.Empty),
                .FormaDePago    = If(comp.Attributes("FormaPago").Any(), comp.Attribute("FormaPago").Value, String.Empty),
                .MetodoDePago   = If(comp.Attributes("MetodoPago").Any(), comp.Attribute("MetodoPago").Value, String.Empty),
                .LugarExpedicion = If(comp.Attributes("LugarExpedicion").Any(), comp.Attribute("LugarExpedicion").Value, String.Empty),
                .Condiciones    = If(comp.Attributes("CondicionesDePago").Any(), comp.Attribute("CondicionesDePago").Value, String.Empty),
                .Sello          = If(comp.Attributes("Sello").Any(), comp.Attribute("Sello").Value, String.Empty),
                .NoCertificado  = If(comp.Attributes("NoCertificado").Any(), comp.Attribute("NoCertificado").Value, String.Empty),
                .Certificado    = If(comp.Attributes("Certificado").Any(), comp.Attribute("Certificado").Value, String.Empty)
            }

            Dim emisorNode = xDoc.Descendants(ns4 + "Emisor").FirstOrDefault()
            If emisorNode IsNot Nothing Then
                fac.Emisor = New EmisorCfdi With {
                    .Rfc          = If(emisorNode.Attributes("Rfc").Any(), emisorNode.Attribute("Rfc").Value, String.Empty),
                    .Nombre       = If(emisorNode.Attributes("Nombre").Any(), emisorNode.Attribute("Nombre").Value, String.Empty),
                    .RegimenFiscal = If(emisorNode.Attributes("RegimenFiscal").Any(), emisorNode.Attribute("RegimenFiscal").Value, String.Empty)
                }
            End If

            Dim receptorNode = xDoc.Descendants(ns4 + "Receptor").FirstOrDefault()
            If receptorNode IsNot Nothing Then
                fac.Receptor = New ReceptorCfdi With {
                    .Rfc                   = If(receptorNode.Attributes("Rfc").Any(), receptorNode.Attribute("Rfc").Value, String.Empty),
                    .Nombre                = If(receptorNode.Attributes("Nombre").Any(), receptorNode.Attribute("Nombre").Value, String.Empty),
                    .UsoCFDI               = If(receptorNode.Attributes("UsoCFDI").Any(), receptorNode.Attribute("UsoCFDI").Value, String.Empty),
                    .DomicilioFiscalReceptor = If(receptorNode.Attributes("DomicilioFiscalReceptor").Any(), receptorNode.Attribute("DomicilioFiscalReceptor").Value, String.Empty),
                    .RegimenFiscalReceptor = If(receptorNode.Attributes("RegimenFiscalReceptor").Any(), receptorNode.Attribute("RegimenFiscalReceptor").Value, String.Empty)
                }
            End If

            fac.Conceptos = New List(Of ConceptoCfdi)
            For Each c In xDoc.Descendants(ns4 + "Concepto")
                fac.Conceptos.Add(New ConceptoCfdi With {
                    .ClaveProdServ = If(c.Attributes("ClaveProdServ").Any(), c.Attribute("ClaveProdServ").Value, String.Empty),
                    .Cantidad      = If(c.Attributes("Cantidad").Any(), c.Attribute("Cantidad").Value, String.Empty),
                    .ClaveUnidad   = If(c.Attributes("ClaveUnidad").Any(), c.Attribute("ClaveUnidad").Value, String.Empty),
                    .Unidad        = If(c.Attributes("Unidad").Any(), c.Attribute("Unidad").Value, String.Empty),
                    .Descripcion   = If(c.Attributes("Descripcion").Any(), c.Attribute("Descripcion").Value, String.Empty),
                    .ValorUnitario = If(c.Attributes("ValorUnitario").Any(), c.Attribute("ValorUnitario").Value, String.Empty),
                    .Importe       = If(c.Attributes("Importe").Any(), c.Attribute("Importe").Value, String.Empty),
                    .Descuento     = If(c.Attributes("Descuento").Any(), c.Attribute("Descuento").Value, String.Empty),
                    .ObjetoImp     = If(c.Attributes("ObjetoImp").Any(), c.Attribute("ObjetoImp").Value, String.Empty)
                })
            Next

            Dim timbreNode = xDoc.Descendants(tfd + "TimbreFiscalDigital").FirstOrDefault()
            If timbreNode IsNot Nothing Then
                fac.Timbre = New TimbreFiscal With {
                    .UUID             = If(timbreNode.Attributes("UUID").Any(), timbreNode.Attribute("UUID").Value, String.Empty),
                    .FechaTimbrado    = If(timbreNode.Attributes("FechaTimbrado").Any(), timbreNode.Attribute("FechaTimbrado").Value, String.Empty),
                    .RfcProvCertif    = If(timbreNode.Attributes("RfcProvCertif").Any(), timbreNode.Attribute("RfcProvCertif").Value, String.Empty),
                    .NoCertificadoSAT = If(timbreNode.Attributes("NoCertificadoSAT").Any(), timbreNode.Attribute("NoCertificadoSAT").Value, String.Empty),
                    .SelloCFD         = If(timbreNode.Attributes("SelloCFD").Any(), timbreNode.Attribute("SelloCFD").Value, String.Empty),
                    .SelloSAT         = If(timbreNode.Attributes("SelloSAT").Any(), timbreNode.Attribute("SelloSAT").Value, String.Empty)
                }
            End If

            Return fac

        Catch ex As Exception
            Return New Cfdi40 With {.EsXMLValido = False}
        End Try
    End Function

    Public Function ProcesarFacturas(rutasXML As IEnumerable(Of String)) As List(Of Cfdi40)
        Dim resultados As New List(Of Cfdi40)

        For Each rutaXML In rutasXML
            Try
                Dim xDoc As XDocument = XDocument.Load(rutaXML)
                Dim factura = ReadComprobante40(xDoc)
                If factura IsNot Nothing Then
                    resultados.Add(factura)
                End If
            Catch ex As Exception
                resultados.Add(New Cfdi40 With {.EsXMLValido = False})
            End Try
        Next

        Return resultados
    End Function

End Class