# Sistema de Gestión de Solicitudes Minfin


# Escenario y propuestas:

El sistema propuesto contará con SQL Server, un framework de Entidad, contará con servicios independientes para:
 - Registro.
 - Consulta.
 - Cambio de estado.
 - Validaciones de negocio.
 - Arquitectura por capas
 - manejo de eventos con RabbitMQ.

# Servicio de Solicitudes:
-Id(small_Int)
-nombe_beneficiario(Varchar(80))
-Descripcion(Varchar(80))
-NIT(Varchar(8))
-Cuenta_Origen(small_Int)
-Cuenta_Destino(small_Int)
-Documento
-Fuente_Financiamiento(Int)
-Estructura_presupuestaria(Int)
-Monto(DECIMAL(18,2))
-Moneda(Varchar(5))
-DatosSensiblesCifrados
-Estado(Bollean)
-FechaCreacion(Date)
-FechaActualizacion(Date)
-CreadoPor(Int(10))

# Servicio de Validaciones:
-Validar estructura presupuestaria.
-Validar fuente de financiamiento.
-Validar beneficiario.
-Validar cuentas.
-Validar disponibilidad o condiciones financieras.
-Consultar sistema legado.

# Servicio de Procesamiento:
-Ejecutar operaciones.
-Orquestar procesos.
-Gestionar estados.
-Coordinar operaciones sincronas/asincronas.

# Servicio de Auditoria:
-Id usuario
--Fecha.
-Origen.
-Data que cambia.
-Estado anterior.
-Estado nuevo.
 -Resultado.
 -Correlation ID.

# Servicio de Reporteria:
-Consultas.
-Reportes.
-Exportaciones.
-Estadísticas.

# Modelado de Estados:
borrador -> Registrado
Registrado -> En_validcion
En_Validacion -> Rechazada
En_Validacion -> Cancelada
En_Validacion -> Validada
Validada -> ENviada_proceso
Enviada_proceso -> En_procesamiento
En_procesamiento -> PRocesada
Procesada -> Pagada


Las transiciones deben estar controladas por reglas de negocio.

# Transiciones Controladas

BORRADOR -> REGISTRADA       (permitido)
REGISTRADA -> EN_VALIDACION  (permitido
PAGADA -> BORRADOR           (NO permitido)
PAGADA -> CANCELADA          solo mediante autorizacion de un nivel superior.

# Tipos de Comunicaion
Se puede tener comunicación síncrona o asincrona.

# comunicación sincrona
Cuando el usuario encesita respuesta inmediata (sistemas internos, portal ciudadano para validar si existe un beneficiario, consulta de catalogos, consulta de estado, que se pueda validar una cuenta). Deberá de manejar timeouts.

propuesta:
Angular ->api gateway -> servicio solicitud -> servicio validacion -> capa de integración -> servicio_legado -> Respuesta

# comunicación Asincrona
Se usaría para operaciones que pueden demorar mas tiempo y no se necesita respuesta inmediata.


Diagrama propuesto:
Solicitud -> servicio_solicitudes -> RabbitMQ -> (Validación, procesamiento, Notificaciones, Auditoria, Reintentos).

# Capa de integración
para que el sistema no conozca las integracioones de los sistemas externos.

Servicios en .NET -> Capa de Integración.
La capa de integración tendrá adaptadores para cada sistema con el que se conecta:
- adapder sistema legado
- adapter banco A
- adapter banco B
- adapter Gubernamentales
- adapter sistema presupuestario
- adapter sistema contabilidad

Cada adapter tiene su propias:
-autenticacion
-transformacion
-Servicio SOAP
-Servicio REST
-certificado
-timeouts
-reintentos
-logs técnicos

# Integracion con servicios SOAP
base .NET ->legacyAdapter
el LegacyAdapter tendra:
|-Cliente SOAP
|-Mapeo del XML
|-Autenticacion
|-Tiemout
|-Manejo de errores ()
|
|-->Sistema legado


# Sistema legado
los microservicios se deben de comunicar con una capa de integración así:

microservicios -> capa de integración -> adapdador de legado -> Sistema Central


# Documentos de respaldo
el servicio se comunicaría con una base de datos de archivos.

Diagrama:
Angular -> API -> servicio de Documenttos -> objeto de almacenamiento (Azure o Interno)

# Datos sensibles y cifrado

Se debe contar con cifrado en tránsito y en reposo.
Los datos sensibles pueden ser:
-Las cuentas bancarias.
- Información de beneficiarios.
- Identificación del beneficiario.
- Documentos adjuntos.
- Credenciales y ttokens.

# Gestion de secrets:
Se debe de manejar un administrador de secrets en algún algoritmo o formato como base64, se deben de almacenar por ejemplo:
-DB_user
-DB_password
-API Keyss
-credenciales de soap
En kubernetes se tiene una sección de Secrets que solo un usuario con privilegios de administrador puede tener opçion de revelarr.

# Procesos batch o bulk
Para las conciliaciones bancarias o procesos de pagos que se quedaron pendientes, actualizaciones de catalgos, para generar reportes, procesos de limpiezas de data temporal.

Se usará un calendarizador o scheduler genera un batch por medio de un worker que se ejecute en un horario fuera de la mayor carga de trabajo. 

Calendarizador -> worker de batch
worker de batch:
-Conciliaciones
-Cargas
-reproceso (cantidad se debe definir)

SOAP-> APIS directos.
procesos batch -> desarrollo de worker.
colas aisladas -> rabbitMQ

# Diagrama de Arquitectura:

Aplicación
 
 * backend
    	 SolicitudSystem.sln
    
    	src
        	   SolicitudSystem.Domain
          	     + Entidades y enums
       
     	SolicitudSystem.Application
          	  + DTOs
           	  +Interfaces
         	  +Reglas de negocio
       
        	SolicitudSystem.Infrastructure
          	  +Entidades Framework
           	  +SQL Server
           	  +RabbitMQ
           	  +JWT
           	  +Cifrado AES-GCM
       
        	SolicitudSystem.Api
           	  +Controllers
           	  +Middleware
           	  +configuración
       
        	SolicitudSystem.Worker
            	  +Consumidor RabbitMQ
    
     	tests
         	  +SolicitudSystem.Tests
 
 * frontend
         +Angular

   * sql
        +01_schema.sql

    * docker-compose.yml
    


uso de Stack: Versiones Angular 22 + .NET 10 + DB SQL Server 2022 + RabbitMQ 4.3.



## Ejecutar con Docker
1. Desde esta carpeta: `docker compose up --build`.
2. Frontend: http://localhost:4200
3. API: http://localhost:5000
4. RabbitMQ: http://localhost:15672 (guest/guest)
5. SSMS: servidor `localhost,1433`, usuario `sa`, password `YourStrong!Passw0rd`.
6. Login inicial: `admin` / `Admin123!`.

La API crea la base y tablas al iniciar. La tabla `MensajesProcesados` tiene índice único para hacer idempotente el consumo de RabbitMQ.

## Desarrollo manual
1. Levantar SQL Server y RabbitMQ.
2. Ajustar `backend/src/SolicitudSystem.Api/appsettings.json`.
3. Ejecutar `dotnet restore backend/SolicitudSystem.sln`.
4. Ejecutar API: `dotnet run --project backend/src/SolicitudSystem.Api`.
5. Ejecutar worker: `dotnet run --project backend/src/SolicitudSystem.Worker`.
6. En frontend: `npm install` y `npm start`.

## Flujo
- POST /api/auth/login obtiene JWT.
- GET /api/solicitudes consulta.
- POST /api/solicitudes registra.
- PUT /api/solicitudes/{id} actualiza.
- PATCH /api/solicitudes/{id}/estado cambia estado.
- Registrar y actualizar publican eventos `solicitud.creada` y `solicitud.actualizada`.
- Worker consume ambos eventos.
- `MessageId` se registra antes de procesar; duplicados se confirman sin reprocesar.
- `DatosSensibles` se guarda cifrado con AES-GCM.
- Middleware devuelve errores HTTP consistentes.

## Regla de estados
Pendiente -> EnProceso o Cancelada
EnProceso -> Resuelta o Cancelada
Resuelta y Cancelada son estados finales.

## Despliegue
Para una actualización con interrupción mínima, usar al menos dos réplicas de API detrás de un reverse proxy/load balancer y actualizar gradualmente. Docker Compose incluido sirve para laboratorio/desarrollo; producción debe añadir TLS, secretos fuera del compose, backups, health checks y observabilidad.

## Manejo de ramas
Se manejan ramas:
-DEV
-QA
-MAIN
De esta manera se puede tener independencia en las configuraciones aplicables a los distintos ambientes, separando seguridad, conexiones a DB, legados.

swagger Auth Login
<img width="1027" height="563" alt="image" src="https://github.com/user-attachments/assets/a018884b-0349-47aa-ae1d-625b3037f49f" />

swagger generación de token:
<img width="1004" height="495" alt="image" src="https://github.com/user-attachments/assets/c1c30ed7-f684-457f-9fd3-0abe717df0a9" />

# RabbitMQ levantado
<img width="1329" height="588" alt="image" src="https://github.com/user-attachments/assets/44da161f-bff0-4bcf-9ab2-b2083fe50bf1" />

# Diagrama de base de datos
<img width="1126" height="910" alt="image" src="https://github.com/user-attachments/assets/e0af0d8f-235b-487e-9034-8db3e46f7bef" />
