pipelineJob('mi-job-ejemplo') {
    description('Job creado correctamente con JCasC')
    definition {
        cps {
            // Fíjate en las tres comillas simples (''') que envuelven el pipeline
            script('''
                pipeline {
                    agent any
                    stages {
                        stage('Hola Mundo') {
                            steps {
                                echo '¡Por fin funcionó!'
                            }
                        }
                    }
                }
            '''.stripIndent())
            sandbox() // Importante para que no pida aprobación manual de scripts
        }
    }
}