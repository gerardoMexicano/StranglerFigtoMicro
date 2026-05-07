pipelineJob('mi-job-ejemplo') {
    description('Job de ejemplo - creado automáticamente vía JCasC + Job DSL')
    definition {
        cps {
            script('''
                pipeline {
                    // Corre en el nodo maestro (built-in)
                    agent { label 'built-in' }
                    stages {
                        stage('Hola Mundo') {
                            steps {
                                echo '¡Por fin funcionó! Jenkins automatizado al 100%'
                            }
                        }
                    }
                }
            '''.stripIndent())
            sandbox(true)
        }
    }
}
