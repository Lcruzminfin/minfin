import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { API_URL } from '../../core/api';
@Component({standalone:true,imports:[FormsModule],template:`
<div class="center"><div class="card login"><h1>Gestión de Solicitudes</h1><h2>Autenticación</h2>
<form (ngSubmit)="login()"><label>Usuario<input name="usuario" [(ngModel)]="usuario" required></label><label>Contraseña<input name="password" type="password" [(ngModel)]="password" required></label><button>Ingresar</button><p class="error">{{error}}</p></form></div></div>`})
export class LoginComponent {
 private http=inject(HttpClient); private router=inject(Router); usuario='admin'; password='Admin123!'; error='';
 login(){this.error='';this.http.post<{token:string}>(`${API_URL}/auth/login`,{usuario:this.usuario,password:this.password}).subscribe({next:r=>{localStorage.setItem('token',r.token);this.router.navigateByUrl('/')},error:e=>this.error=e.error?.error??'No fue posible iniciar sesión.'});}
}
