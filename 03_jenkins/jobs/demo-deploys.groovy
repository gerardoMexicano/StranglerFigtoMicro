pipelineJob('demo-deploy'){
    agent any
    triggers {
        cron('H/2 * * * *')
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
            sh """
                cd ${LAB_PATH}
                docker compose logs --tail=50
            """
        }
    }
}