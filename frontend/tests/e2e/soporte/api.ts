import { expect, request, type APIRequestContext, type APIResponse } from '@playwright/test'
import { leerEntorno, type EntornoE2E } from './entorno.ts'

/** Cliente de la API real para los escenarios de quickstart.md. */
export class ClienteApi {
  readonly entorno: EntornoE2E
  readonly usuario: { id: string; correo: string }
  private contexto: APIRequestContext | null = null

  private constructor(entorno: EntornoE2E, usuario: { id: string; correo: string }) {
    this.entorno = entorno
    this.usuario = usuario
  }

  static async iniciarComo(quien: 'admin' | 'ajeno'): Promise<ClienteApi> {
    const entorno = leerEntorno()
    const cliente = new ClienteApi(entorno, entorno[quien])
    await cliente.iniciarSesion()
    return cliente
  }

  /** Paso 1 de quickstart §5: login real; el alcance viaja en el token (RF-074, RF-077). */
  async iniciarSesion(): Promise<{
    accessToken: string
    rol: 'GLOBAL_ADMINISTRATOR' | 'COMPANY_ADMINISTRATOR' | null
    companiaIds: string[]
  }> {
    const anonimo = await request.newContext({ baseURL: this.entorno.apiUrl })
    const respuesta = await anonimo.post('/api/auth/login', {
      data: { correo: this.usuario.correo, password: this.entorno.password },
    })

    expect(respuesta.status(), await respuesta.text()).toBe(200)
    const cuerpo = (await respuesta.json()) as {
      accessToken: string
      rol: 'GLOBAL_ADMINISTRATOR' | 'COMPANY_ADMINISTRATOR' | null
      companiaIds: string[]
    }
    await anonimo.dispose()

    await this.contexto?.dispose()
    this.contexto = await request.newContext({
      baseURL: this.entorno.apiUrl,
      extraHTTPHeaders: { Authorization: `Bearer ${cuerpo.accessToken}` },
    })

    return cuerpo
  }

  async get(ruta: string): Promise<APIResponse> {
    return this.ctx().get(ruta)
  }

  async post(ruta: string, datos?: unknown): Promise<APIResponse> {
    return this.ctx().post(ruta, { data: datos ?? {} })
  }

  async put(ruta: string, datos: unknown): Promise<APIResponse> {
    return this.ctx().put(ruta, { data: datos })
  }

  /** Ejecuta la petición y exige el código indicado, devolviendo el cuerpo. */
  async exigir<T>(respuesta: Promise<APIResponse>, estado: number): Promise<T> {
    const r = await respuesta
    expect(r.status(), `${r.url()} → ${await r.text()}`).toBe(estado)
    const texto = await r.text()
    return (texto ? JSON.parse(texto) : undefined) as T
  }

  /** Código de negocio del ProblemDetails de una respuesta de error. */
  async codigo(respuesta: Promise<APIResponse>, estado: number): Promise<string | undefined> {
    const cuerpo = await this.exigir<{ codigo?: string }>(respuesta, estado)
    return cuerpo?.codigo
  }

  /**
   * Crea una compañía dentro del alcance del usuario (quickstart §5 paso 2).
   *
   * Con el modelo RBAC (D1) el usuario `admin` es GLOBAL_ADMINISTRATOR, así que su alcance cubre
   * toda compañía —incluidas las que se creen después— sin enumerarlas y sin volver a iniciar
   * sesión. El flujo anterior, que se añadía compañías a sí mismo, es justo la autoelevación que
   * RF-076 prohíbe y que CS-036 verifica.
   */
  async crearCompaniaEnAlcance(
    nombre: string,
    tipoCompania: 'PRINCIPAL_MANDANTE' | 'CONTRATISTA',
  ): Promise<string> {
    const ruc = await this.maestro('tipos-documento', 'RUC')

    const compania = await this.exigir<{ id: string }>(
      this.post('/api/companias', {
        nombre: `${nombre} ${Date.now()}`,
        tipoDocumentoId: ruc,
        numeroDocumento: aleatorio(11),
        tipoCompania,
        estado: 'ACTIVO',
        // RF-080: obligatoria para una Principal, sin uso funcional para una Contratista.
        zonaHorariaIana: tipoCompania === 'PRINCIPAL_MANDANTE' ? 'America/Lima' : null,
      }),
      201,
    )

    await this.exigir(this.get(`/api/companias/${compania.id}`), 200)

    return compania.id
  }

  /** Identificador de un valor de catálogo maestro por nombre (semilla versionada o creado). */
  async maestro(catalogo: string, nombre?: string): Promise<string> {
    const valores = await this.exigir<{ id: string; nombre: string; estado: string }[]>(
      this.get(`/api/maestros/${catalogo}`),
      200,
    )

    const valor = valores.find((v) => v.estado === 'ACTIVO' && (!nombre || v.nombre === nombre))
    expect(valor, `${catalogo}: ${nombre ?? 'cualquier valor activo'}`).toBeDefined()

    return valor!.id
  }

  async crearMaestro(catalogo: string, nombre: string): Promise<string> {
    const creado = await this.exigir<{ id: string }>(
      this.post(`/api/maestros/${catalogo}`, {
        nombre: `${nombre} ${Date.now()}`,
        estado: 'ACTIVO',
      }),
      201,
    )
    return creado.id
  }

  async crearPersona(nombres: string, apellidos: string): Promise<string> {
    const persona = await this.exigir<{ id: string }>(
      this.post('/api/personas', {
        nombres,
        apellidos,
        fechaNacimiento: '1988-04-12',
        tipoDocumentoId: await this.maestro('tipos-documento', 'DNI'),
        numeroDocumento: aleatorio(8),
        generoId: await this.maestro('generos'),
        correoElectronico: `${aleatorio(10)}@e2e.cl`,
        tipoSangreId: await this.maestro('tipos-sangre'),
        contactoEmergencia: 'Contacto E2E',
        numeroEmergencia: '+51 999 000 111',
      }),
      201,
    )
    return persona.id
  }

  async cerrar(): Promise<void> {
    await this.contexto?.dispose()
  }

  private ctx(): APIRequestContext {
    if (!this.contexto) {
      throw new Error('Sesión no iniciada')
    }
    return this.contexto
  }
}

function aleatorio(digitos: number): string {
  return Array.from({ length: digitos }, () => Math.floor(Math.random() * 10)).join('')
}
