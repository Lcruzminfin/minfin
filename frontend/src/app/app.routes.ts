import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login.component';
import { SolicitudesComponent } from './features/solicitudes/solicitudes.component';
export const routes: Routes = [
 { path:'login', component:LoginComponent },
 { path:'', component:SolicitudesComponent },
 { path:'**', redirectTo:'' }
];
