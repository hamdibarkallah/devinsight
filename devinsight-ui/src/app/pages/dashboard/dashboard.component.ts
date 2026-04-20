import { Component, OnInit } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, DecimalPipe],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  user: any;
  repos: any[] = [];
  selectedRepoId = '';
  selectedRepo: any = null;
  from = '2020-01-01';
  to = new Date().toISOString().split('T')[0];

  velocity: any = null;
  devStats: any[] = [];
  trends: any[] = [];
  anomalies: any[] = [];
  bottlenecks: any[] = [];

  loading = false;
  syncing = false;
  syncMsg = '';
  githubToken = '';
  showTokenInput = false;
  integrations: any[] = [];

  constructor(private api: ApiService, private auth: AuthService, private router: Router) {}

  ngOnInit() {
    this.user = this.auth.currentUser$.value;
    this.loadIntegrations();
    this.loadRepos();
  }

  loadIntegrations() {
    this.api.listIntegrations().subscribe({ next: v => { this.integrations = v; } });
  }

  loadRepos() {
    this.api.listRepos().subscribe({ next: repos => {
      this.repos = repos;
      if (repos.length) {
        this.selectedRepoId = repos[0].id;
        this.onRepoChange();
      }
    }});
  }

  onRepoChange() {
    this.selectedRepo = this.repos.find(r => r.id === this.selectedRepoId);
    // Auto-sync if no data yet
    this.api.getVelocity(this.selectedRepoId, this.from, this.to).subscribe({ next: v => {
      if (v.totalCommits === 0) {
        this.syncRepo();
      } else {
        this.loadAnalytics();
      }
    }, error: () => this.loadAnalytics() });
  }

  loadAnalytics() {
    if (!this.selectedRepoId) return;
    this.loading = true;
    const id = this.selectedRepoId;
    this.api.getVelocity(id, this.from, this.to).subscribe({ next: v => this.velocity = v });
    this.api.getDeveloperStats(id, this.from, this.to).subscribe({ next: v => this.devStats = v });
    this.api.getTrends(id, this.from, this.to).subscribe({ next: v => { this.trends = v.filter((t: any) => t.commits > 0); this.loading = false; } });
    this.api.getAnomalies(id, 90).subscribe({ next: v => this.anomalies = v.anomalies || [] });
    this.api.getBottlenecks(id).subscribe({ next: v => this.bottlenecks = v });
  }

  connectGitHub() {
    if (!this.githubToken) return;
    this.api.addGitHub(this.githubToken).subscribe({
      next: () => { this.syncMsg = '✅ GitHub connected! Syncing repos...'; this.showTokenInput = false; this.githubToken = ''; this.loadIntegrations(); this.syncRepos(); },
      error: (e) => {
        if (e.status === 409) {
          // Already connected — update the token instead then sync
          this.api.updateToken(this.integrations.find((i: any) => i.provider === 'GitHub')?.id, this.githubToken).subscribe({
            next: () => { this.syncMsg = '✅ Token updated! Syncing repos...'; this.showTokenInput = false; this.githubToken = ''; this.loadIntegrations(); this.syncRepos(); },
            error: () => { this.syncMsg = '✅ Already connected. Syncing...'; this.showTokenInput = false; this.githubToken = ''; this.syncRepos(); }
          });
        } else {
          this.syncMsg = '❌ ' + (e.error?.message || 'Failed');
        }
      }
    });
  }

  syncRepos() {
    this.syncing = true; this.syncMsg = '⏳ Syncing repositories...';
    this.api.syncRepos('github').subscribe({
      next: (r) => { this.syncMsg = `✅ ${r.message}`; this.loadRepos(); this.syncing = false; },
      error: () => { this.syncMsg = '❌ Sync failed'; this.syncing = false; }
    });
  }

  syncRepo() {
    if (!this.selectedRepoId) return;
    this.syncing = true; this.syncMsg = '⏳ Syncing commits & PRs...';
    this.api.syncCommits(this.selectedRepoId).subscribe({
      next: (c) => {
        this.api.syncPRs(this.selectedRepoId).subscribe({
          next: (p) => {
            this.syncMsg = `✅ ${c.message} | ${p.message}`;
            this.syncing = false;
            this.loadAnalytics();
          },
          error: () => { this.syncing = false; this.loadAnalytics(); }
        });
      },
      error: () => { this.syncing = false; this.loadAnalytics(); }
    });
  }

  logout() { this.auth.logout(); this.router.navigate(['/login']); }

  severityClass(s: string) { return s === 'Warning' ? 'warn' : 'info'; }

  get hasGitHub() { return this.integrations.some(i => i.provider === 'GitHub'); }

  get maxTrendCommits(): number {
    return Math.max(...this.trends.map((t: any) => t.commits), 1);
  }

  get totalTrendCommits(): number {
    return this.trends.reduce((sum: number, t: any) => sum + t.commits, 0);
  }

  barHeight(commits: number): number {
    return Math.max(4, Math.round((commits / this.maxTrendCommits) * 100));
  }
}
