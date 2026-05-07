pipelineJob('saludo-jenkins') {
    description('Job que lee un archivo directamente del disco del servidor')
    definition {
        cps {
            // Jenkins va al disco duro, abre el archivo y extrae el texto
            String pipelineCode = new File('/var/jenkins_home/jobs/saludo-job.groovy').text
            
            script(pipelineCode)
            
            // IMPORTANTE: Al leer archivos del disco, por seguridad de Jenkins, 
            // generalmente debes apagar el sandbox para que no bloquee la acción.
            sandbox(false) 
        }
    }
}