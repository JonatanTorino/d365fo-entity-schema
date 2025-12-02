# Cómo ver los cambios en tu repositorio de GitHub

Sigue estos pasos para comprobar en GitHub los cambios que ya están en esta rama local:

1. **Verifica qué remoto usar.** Si ya tienes un remoto que apunta a tu repositorio de GitHub, debería llamarse `origin`. Compruébalo con:
   ```bash
   git remote -v
   ```
2. **Envía la rama actual a GitHub.** Asegúrate de estar en la rama correcta (por ejemplo `work`) y haz push a tu repositorio:
   ```bash
   git push origin work
   ```
   Si es la primera vez que subes esta rama, Git te indicará si debes crearla en el remoto.
3. **Abre la rama en GitHub.** Ve a la URL de tu repositorio de GitHub y selecciona la rama `work` en el selector de ramas. Podrás ver los archivos recién agregados en la vista del repositorio.
4. **Revisa el historial y el diff.**
   - Historial de commits: pestaña **Commits** en GitHub para ver el commit más reciente.
   - Comparar cambios: usa **Compare & pull request** o el enlace **Compare** para revisar el diff entre `work` y la rama principal (por ejemplo `main`).
5. **Crear un pull request (opcional).** Si quieres que los cambios se integren en `main`, crea un Pull Request desde la rama `work` y revisa el diff que GitHub muestra automáticamente.

> Nota: si tu remoto no está configurado o tiene otro nombre, usa `git remote add origin <URL-de-tu-repo>` para apuntarlo a GitHub antes de hacer push.
