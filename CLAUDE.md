# Instrucciones para Claude en este repositorio

Proyecto Unity 6 (URP) del juego Kurogane Touge, para Android y PC. Ver `README.md`.

## Política de git

- Nunca hacer `commit`, `push`, `reset`, `rebase` ni force-push directo sobre `main` o `develop`.
- Cada cambio va en una rama aparte con nombre descriptivo (`feat/...`, `fix/...`). La integración a `main` se hace con un PR o un merge confirmado por el usuario.
- Mensajes en Conventional Commits (`tipo(alcance): descripción`), con cuerpo en español explicando qué y por qué.
- No mencionar a Claude ni a Anthropic, ni agregar firmas `Co-Authored-By`, en commits, PRs o comentarios.

## Convenciones de código

- C#, namespaces `Kurogane.*`. No crear namespaces llamados `Camera`, `Input` ni `Editor` dentro de `Kurogane`: ocultan los tipos de Unity (por eso existen `Kurogane.CameraRig`, `Kurogane.Controls` y `Kurogane.EditorTools`).
- Ejes: rumbo 0 = +Z, positivo = derecha; lateral positivo = derecha.
- La física del coche (`CarController`) es un port 1:1 del prototipo web. Si se cambian números, que sea a propósito y probándolo en el juego.
- Textos del juego en español.
