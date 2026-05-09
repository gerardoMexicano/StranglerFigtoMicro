pipelineJob('demo-deploy') {
    description('Job de demo deploy - creado automáticamente vía JCasC + Job DSL')
    definition {
        cps {
            script('''
                pipeline {
                    // Corre en el nodo maestro (built-in)
                    // Para correr en un agente específico usar: agent { label 'linux docker' }
                    agent { label 'built-in' }
                    triggers {
                        cron('H/30 * * * *')
                    }
                    environment {
                        LAB_PATH = '/home/gerarddebian/Documentos/Proyectos/LAboratorio'
                    }
                    stages {
                        stage('Verificar Laboratorio') {
                            steps {
                                sh """
                                    ls -la ${LAB_PATH}/
                                    cat ${LAB_PATH}/docker-compose.yml
                                """
                            }
                        }
                        stage('Levantar Servicios') {
                            steps {
                                sh """
                                    cd ${LAB_PATH}
                                    docker compose up -d --build
                                    docker compose ps
                                """
                            }
                        }
                        stage('Probar API Gateway') {
                            steps {
                                sh """
                                    sleep 3
                                    curl -s http://localhost:8082/ || echo "Esperando servicios..."
                                """
                            }
                        }
                    }
                    post {
                        failure {
                            sh "cd ${LAB_PATH} && docker compose logs --tail=50"
                        }
                    }
                }
            '''.stripIndent())
            sandbox(true)
        }
    }
}
