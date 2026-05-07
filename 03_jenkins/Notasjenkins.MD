## HOLA SALUDOS
# NOTA Como Funciona el proyecto y jsutificacion o idea

la idea en si de este proyecto comenzo solo siendo una forma de migrar con straglerfig unas antiguas aplicacions de .net framewok 
pero se ha vuelto un laboratorio muy divertido y ha crecido me he divertido bastantet en el desarrollo  esta es la primera nota que escribo a dia 04/05/2026

- bien primero que nada la intencion era que todo el proyecto se levantara con un script  pero  bueno no siempre salen las cosas como deseas
- Es posible crearlo  en una maquina virtual y que un script gestione todo el servidor auqnue este en una maquina vitual si si es posible
- pero aqui hay matices importantes si aun no migras la aplicacion no no es posible que un solo script levante todo para hacerlo asi tendria que ejecutarse en el mismo servidor  podrias intentarlo pero quizas no sea replicable 
- los  Agentes de jenkins parecen mejor opcion  una instalacion java y configuracion y ejecuccion casi nativa o almenos eso promete

07/05/2026
- bien estuve probando las configuraciones basicas y si funciona pero hay un detalle que aprendimos y acerca de como debemos trabajar los jobs mas que por entorno quizas por completo como interactua la aplicacion en completo sobre todo al momento de la integracion y automatizacion si la idea es vender una aplicacion con testing y produccion no creas dos jobs creas uno que orquesta ademas de intentar tener todo versionado con scripts asi si cambiamos de jenkins a otra plataforma no hay tanto problema o almenos esa es la idea o almenos eso creemos