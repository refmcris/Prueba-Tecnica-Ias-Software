# Uso de IA

Usé IA para revisar la prueba compartida y contrastar decisiones sobre la organización, la persistencia y el procesamiento. Las propuestas se revisaron durante el desarrollo y la decisión final sobre qué mantener o cambiar fue mía.

La asistencia incluyó código del backend y del frontend, el diseño visual de la pantalla, la integración con la API y pruebas automatizadas. También hubo apoyo en la revisión del código de la solución y de los fragmentos legados, así como en la redacción y revisión de esta documentación. La interfaz se ajustó durante la revisión para mantener una presentación sencilla y mensajes en español.

Los resultados se comprobaron con la compilación de ambos proyectos, las pruebas y la ejecución conjunta de API, Worker y React. Se revisaron especialmente los duplicados concurrentes, los reintentos y la recuperación cuando el proveedor guarda un efecto pero se pierde la confirmación. En la revisión pasaron 112 pruebas de .NET y 37 de React.

Se corrigieron propuestas durante el proceso. La API se organizó con controladores y DTOs separados, la configuración se dejó en los mecanismos habituales de .NET y se simplificaron los mensajes y la interfaz. También se revisó la explicación de idempotencia para distinguir una llamada repetida de un efecto duplicado.
