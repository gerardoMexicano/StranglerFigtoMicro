pipeline {
    agent any
    
    stages {
        stage('Saludo') {
            steps {
                echo 'HOLA MUNDO'
            }
        }
    }
}