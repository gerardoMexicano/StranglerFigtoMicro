pipelineJob('demo-deploys-jenkins') {
    description('Job que lee un archivo demo-deploys.groovy directamente del disco del jenkins')
    definition {
        cps {
            // Jenkins va al disco duro, abre el archivo y extrae el texto
            String pipelineCode = new File('/var/jenkins_home/jobs/demo-deploys.groovy').text
            script(pipelineCode)
            sandbox(false) 
        }
    }
}