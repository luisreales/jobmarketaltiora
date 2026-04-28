import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { OpportunityService } from './opportunity.service';
import { environment } from '../../environments/environment';
import { Opportunity, PagedMarketResponse, ProductSuggestion } from '../models/market.models';

const BASE = `${environment.apiUrl}/api`;

const mockOpportunity: Opportunity = {
  id: 1,
  jobId: 10,
  company: 'Acme',
  jobTitle: 'Cloud Architect',
  llmStatus: 'pending',
  createdAt: new Date().toISOString()
};

const mockPagedResponse: PagedMarketResponse<Opportunity> = {
  items: [mockOpportunity],
  page: 1,
  pageSize: 20,
  totalCount: 1,
  totalPages: 1,
  sortBy: 'createdAt',
  sortDirection: 'desc'
};

describe('OpportunityService', () => {
  let service: OpportunityService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        OpportunityService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service  = TestBed.inject(OpportunityService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getOpportunities — sends GET to /api/opportunities', () => {
    service.getOpportunities().subscribe(res => {
      expect(res.items.length).toBe(1);
      expect(res.items[0].company).toBe('Acme');
    });

    const req = httpMock.expectOne(`${BASE}/opportunities`);
    expect(req.request.method).toBe('GET');
    req.flush(mockPagedResponse);
  });

  it('getOpportunities — passes llmStatus query param', () => {
    service.getOpportunities({ llmStatus: 'pending' }).subscribe();

    const req = httpMock.expectOne(r => r.url === `${BASE}/opportunities`);
    expect(req.request.params.get('llmStatus')).toBe('pending');
    req.flush(mockPagedResponse);
  });

  it('getOpportunity — sends GET to /api/opportunities/:id', () => {
    service.getOpportunity(1).subscribe(o => {
      expect(o.company).toBe('Acme');
    });

    const req = httpMock.expectOne(`${BASE}/opportunities/1`);
    expect(req.request.method).toBe('GET');
    req.flush(mockOpportunity);
  });

  it('synthesizeIdeas — sends POST to /api/opportunities/:id/synthesize-ideas', () => {
    const withIdeas: Opportunity = {
      ...mockOpportunity,
      llmStatus: 'completed',
      productIdeasJson: '[{"name":"Audit Bot","shortTechnicalDescription":"Automates audits."}]'
    };

    service.synthesizeIdeas(1).subscribe(o => {
      expect(o.llmStatus).toBe('completed');
    });

    const req = httpMock.expectOne(`${BASE}/opportunities/1/synthesize-ideas`);
    expect(req.request.method).toBe('POST');
    req.flush(withIdeas);
  });

  it('createFromJob — sends POST to /api/jobs/jobs/:jobId/create-opportunity', () => {
    service.createFromJob(10).subscribe(o => {
      expect(o.jobId).toBe(10);
    });

    const req = httpMock.expectOne(`${BASE}/jobs/jobs/10/create-opportunity`);
    expect(req.request.method).toBe('POST');
    req.flush(mockOpportunity);
  });

  it('createProductFromOpportunity — sends POST to /api/products/from-opportunity', () => {
    const mockProduct: Partial<ProductSuggestion> = {
      id: 99,
      productName: 'Audit Bot',
      opportunityType: 'Manual'
    };
    const request = {
      opportunityId: 1,
      name: 'Audit Bot',
      shortTechnicalDescription: 'Automates audits.'
    };

    service.createProductFromOpportunity(request).subscribe(p => {
      expect(p.productName).toBe('Audit Bot');
    });

    const req = httpMock.expectOne(`${BASE}/products/from-opportunity`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(mockProduct);
  });
});
