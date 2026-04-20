import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  // Integrations
  addGitHub(token: string) { return this.http.post('/api/integrations/github', { personalAccessToken: token }); }
  updateToken(integrationId: string, token: string) { return this.http.put(`/api/integrations/${integrationId}`, { personalAccessToken: token }); }
  listIntegrations() { return this.http.get<any[]>('/api/integrations'); }

  // Sync
  syncRepos(provider = 'github') { return this.http.post<any>(`/api/sync/repos/${provider}`, {}); }
  syncCommits(repoId: string) { return this.http.post<any>(`/api/sync/commits/${repoId}`, {}); }
  syncPRs(repoId: string) { return this.http.post<any>(`/api/sync/pull-requests/${repoId}`, {}); }
  listRepos() { return this.http.get<any[]>('/api/sync/repos'); }

  // Analytics
  getDeveloperStats(repoId: string, from: string, to: string) {
    return this.http.get<any[]>(`/api/analytics/developers/${repoId}?from=${from}&to=${to}`);
  }
  getVelocity(repoId: string, from: string, to: string) {
    return this.http.get<any>(`/api/analytics/velocity/${repoId}?from=${from}&to=${to}`);
  }
  getTrends(repoId: string, from: string, to: string) {
    return this.http.get<any[]>(`/api/analytics/trends/${repoId}?from=${from}&to=${to}`);
  }
  getCycleTime(repoId: string, from: string, to: string) {
    return this.http.get<any>(`/api/analytics/cycle-time/${repoId}?from=${from}&to=${to}`);
  }
  getBottlenecks(repoId: string) {
    return this.http.get<any[]>(`/api/analytics/bottlenecks/${repoId}`);
  }
  getAnomalies(repoId: string, lookbackDays = 90) {
    return this.http.get<any>(`/api/anomaly/${repoId}?lookbackDays=${lookbackDays}`);
  }
}
