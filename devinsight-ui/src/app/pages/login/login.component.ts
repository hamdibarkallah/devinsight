import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  mode: 'login' | 'register' = 'login';
  email = ''; password = ''; displayName = ''; orgName = '';
  error = ''; loading = false;

  constructor(private auth: AuthService, private router: Router) {}

  submit() {
    this.error = ''; this.loading = true;
    const obs = this.mode === 'login'
      ? this.auth.login(this.email, this.password)
      : this.auth.register(this.email, this.password, this.displayName, this.orgName);
    obs.subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (e) => { this.error = e.error?.message || 'An error occurred.'; this.loading = false; }
    });
  }
}
