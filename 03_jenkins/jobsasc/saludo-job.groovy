pipelineJob('saludo-job') {
    description('Job de saludo - creado automáticamente vía JCasC + Job DSL')
    definition {
        cps {
            script('''
                pipeline {
                    // Corre en el nodo maestro (built-in)
                    agent { label 'built-in' }
                    stages {
                        stage('Saludo') {
                            steps {
                                echo 'HOLA MUNDO desde Jenkins automatizado!'
                            }
                        }
                    }
                }
            '''.stripIndent())
            sandbox(true)
        }
    }
}
