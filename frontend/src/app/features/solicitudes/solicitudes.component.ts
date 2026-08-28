import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { API_URL } from '../../core/api';

interface Documento { id:string; nombreArchivo:string; contentType:string; tamano:number; fechaCreacionUtc:string; }
interface Solicitud {
 id:string; titulo:string; descripcion:string; datosSensibles:string; nombreBeneficiario:string; nit:string;
 cuentaOrigen:string; cuentaDestino:string; fuenteFinanciamiento:string; estructuraPresupuestaria:string; monto:number; moneda:string;
 estado:string; fechaCreacionUtc:string; fechaActualizacionUtc:string; creadoPor:string; documentos:Documento[];
}

@Component({standalone:true,imports:[FormsModule,CommonModule],template:`
<header><h1>Solicitudes de pago</h1><nav><a (click)="nuevo()">Nueva solicitud</a><a (click)="cargar()">Consultar</a><a (click)="logout()">Salir</a></nav></header>
<main>
<div class="card"><h2>{{editando ? 'Actualizar solicitud de pago':'Registrar solicitud de pago'}}</h2>
<form (ngSubmit)="guardar()">
<div class="form-grid">
<label>Título<input name="titulo" [(ngModel)]="form.titulo" minlength="5" maxlength="100" required></label>
<label>Nombre de beneficiario<input name="nombreBeneficiario" [(ngModel)]="form.nombreBeneficiario" maxlength="200" required></label>
<label>NIT<input name="nit" [(ngModel)]="form.nit" maxlength="50" required></label>
<label>Cuenta origen<input name="cuentaOrigen" [(ngModel)]="form.cuentaOrigen" maxlength="100" required></label>
<label>Cuenta destino<input name="cuentaDestino" [(ngModel)]="form.cuentaDestino" maxlength="100" required></label>
<label>Fuente de financiamiento<select name="fuenteFinanciamiento" [(ngModel)]="form.fuenteFinanciamiento" required><option value="">Seleccione...</option><option *ngFor="let x of fuentesFinanciamiento" [value]="x">{{x}}</option></select></label>
<label>Estructura presupuestaria<select name="estructuraPresupuestaria" [(ngModel)]="form.estructuraPresupuestaria" required><option value="">Seleccione...</option><option *ngFor="let x of estructurasPresupuestarias" [value]="x">{{x}}</option></select></label>
<label>Monto<input name="monto" type="number" min="0.01" step="0.01" [(ngModel)]="form.monto" required></label>
<label>Moneda<select name="moneda" [(ngModel)]="form.moneda" required><option value="Q">Q</option><option value="$">$</option></select></label>
<label class="full">Descripción<textarea name="descripcion" [(ngModel)]="form.descripcion" minlength="10" maxlength="1000" required></textarea></label>
<label class="full">Dato sensible<input name="dato" [(ngModel)]="form.datosSensibles"></label>
</div>
<div class="documents"><label>Documentos <input type="file" multiple (change)="seleccionarArchivos($event)" accept=".pdf,.png,.jpg,.jpeg,.doc,.docx,.xls,.xlsx"></label><small>PDF, Word, Excel, PNG o JPG. Máximo 10 MB por archivo.</small><ul><li *ngFor="let f of archivosPendientes">{{f.name}} ({{formatearTamano(f.size)}})</li></ul></div>
<div><button>{{editando?'Actualizar':'Registrar'}}</button><button type="button" (click)="nuevo()">Limpiar</button></div><p class="error">{{error}}</p>
</form></div>

<div class="card"><h2>Solicitudes de pago</h2><div class="table-wrap"><table><thead><tr><th>Beneficiario</th><th>NIT</th><th>Monto</th><th>Moneda</th><th>Estado</th><th>Documentos</th><th>Acciones</th></tr></thead><tbody>
<tr *ngFor="let s of solicitudes"><td>{{s.nombreBeneficiario}}</td><td>{{s.nit}}</td><td>{{s.monto | number:'1.2-2'}}</td><td>{{s.moneda}}</td><td>{{s.estado}}</td><td><div *ngFor="let d of s.documentos"><a (click)="descargar(s.id,d)">{{d.nombreArchivo}}</a><button class="danger small" (click)="eliminarDocumento(s.id,d.id)">Eliminar</button></div></td><td><button (click)="editar(s)">Editar</button><select [value]="s.estado" (change)="cambiarEstado(s.id,$any($event.target).value)"><option value="Pendiente">Pendiente</option><option value="EnProceso">EnProceso</option><option value="Resuelta">Resuelta</option><option value="Cancelada">Cancelada</option></select></td></tr></tbody></table></div></div>
</main>`})
export class SolicitudesComponent {
 private http=inject(HttpClient); private router=inject(Router); solicitudes:Solicitud[]=[]; editando=false; error=''; id=''; archivosPendientes:File[]=[];
 fuentesFinanciamiento=['Fuente 11','Fuente 12','Fuente 21','Fuente 22','Fuente 31'];
 estructurasPresupuestarias=['Estructura 1','Estructura 2','Estructura 3','Estructura 4'];
 form={titulo:'',descripcion:'',datosSensibles:'',nombreBeneficiario:'',nit:'',cuentaOrigen:'',cuentaDestino:'',fuenteFinanciamiento:'',estructuraPresupuestaria:'',monto:0,moneda:'Q'};
 constructor(){this.cargar()}
 cargar(){this.http.get<Solicitud[]>(`${API_URL}/solicitudes`).subscribe({next:r=>this.solicitudes=r,error:e=>{if(e.status===401)this.router.navigateByUrl('/login');else this.error=e.error?.error??'Error al consultar.'}})}
 nuevo(){this.editando=false;this.id='';this.archivosPendientes=[];this.form={titulo:'',descripcion:'',datosSensibles:'',nombreBeneficiario:'',nit:'',cuentaOrigen:'',cuentaDestino:'',fuenteFinanciamiento:'',estructuraPresupuestaria:'',monto:0,moneda:'Q'};this.error=''}
 editar(s:Solicitud){this.editando=true;this.id=s.id;this.archivosPendientes=[];this.form={titulo:s.titulo,descripcion:s.descripcion,datosSensibles:s.datosSensibles,nombreBeneficiario:s.nombreBeneficiario,nit:s.nit,cuentaOrigen:s.cuentaOrigen,cuentaDestino:s.cuentaDestino,fuenteFinanciamiento:s.fuenteFinanciamiento,estructuraPresupuestaria:s.estructuraPresupuestaria,monto:s.monto,moneda:s.moneda};this.error='';scrollTo(0,0)}
 seleccionarArchivos(event:Event){const input=event.target as HTMLInputElement;this.archivosPendientes=Array.from(input.files??[])}
 guardar(){this.error='';const req=this.editando?this.http.put<Solicitud>(`${API_URL}/solicitudes/${this.id}`,this.form):this.http.post<Solicitud>(`${API_URL}/solicitudes`,this.form);req.subscribe({next:r=>{const solicitudId=this.editando?this.id:r.id;this.subirDocumentos(solicitudId)},error:e=>this.error=e.error?.error??'Error al guardar.'})}
 subirDocumentos(solicitudId:string){if(!this.archivosPendientes.length){this.cargar();this.nuevo();return}let pendientes=this.archivosPendientes.slice();const siguiente=()=>{const file=pendientes.shift();if(!file){this.cargar();this.nuevo();return}const fd=new FormData();fd.append('file',file,file.name);this.http.post(`${API_URL}/solicitudes/${solicitudId}/documentos`,fd).subscribe({next:()=>siguiente(),error:e=>{this.error=e.error?.error??'La solicitud se guardó, pero un documento no pudo cargarse.';this.cargar()}})};siguiente()}
 cambiarEstado(id:string,estado:string){const estados:any={Pendiente:0,EnProceso:1,Resuelta:2,Cancelada:3};const valor=estado in estados?estados[estado]:Number(estado);this.http.patch(`${API_URL}/solicitudes/${id}/estado`,{estado:valor}).subscribe({next:()=>this.cargar(),error:e=>{this.error=e.error?.error??'Transición no permitida.';this.cargar()}})}
 descargar(solicitudId:string,d:Documento){this.http.get(`${API_URL}/solicitudes/${solicitudId}/documentos/${d.id}/download`,{responseType:'blob'}).subscribe(blob=>{const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=d.nombreArchivo;a.click();URL.revokeObjectURL(url)})}
 eliminarDocumento(solicitudId:string,documentId:string){this.http.delete(`${API_URL}/solicitudes/${solicitudId}/documentos/${documentId}`).subscribe({next:()=>this.cargar(),error:e=>this.error=e.error?.error??'No se pudo eliminar el documento.'})}
 formatearTamano(bytes:number){return bytes<1024?`${bytes} B`:bytes<1048576?`${(bytes/1024).toFixed(1)} KB`:`${(bytes/1048576).toFixed(1)} MB`}
 logout(){localStorage.removeItem('token');this.router.navigateByUrl('/login')}
}
