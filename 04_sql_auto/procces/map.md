Este es el mapa de ruta exacto de lo que vamos a construir:
1.Habilitar HADR en el motor:Configuración de instancia.Encender la característica de Always On a nivel interno en ambos contenedores.
2.Crear llaves maestras y certificados:Generar un certificado de seguridad único en cada nodo para cifrar la comunicación en la red.
3.Intercambiar certificados:Aprovechando la carpeta compartida en P:.Hacer que el nodo primario confíe en el certificado del secundario y viceversa.
4.Crear los Endpoints:Abrir el puerto de comunicación dedicado a la réplica (usualmente el 5022) en ambos motores.
5.Construir el Contained AG:Ejecutar el script de creación del grupo con la cláusula CONTAINED e inicializar las bases de datos.