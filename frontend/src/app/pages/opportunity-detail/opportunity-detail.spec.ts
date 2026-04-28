import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { OpportunityDetail } from './opportunity-detail';
import { OpportunityService } from '../../services/opportunity.service';
import { Opportunity } from '../../models/market.models';

const PENDING_OPP: Opportunity = {
  id: 1,
  jobId: 10,
  company: 'Globex',
  jobTitle: 'Cloud Architect',
  jobDescription: 'Migrate legacy systems to Azure.',
  llmStatus: 'pending',
  createdAt: new Date().toISOString()
};

const COMPLETED_OPP: Opportunity = {
  ...PENDING_OPP,
  llmStatus: 'completed',
  productIdeasJson: JSON.stringify([
    { name: 'Audit Bot',   shortTechnicalDescription: 'Automates code review.'         },
    { name: 'CI/CD Suite', shortTechnicalDescription: 'Automates deployment pipelines.' },
    { name: 'Cost Watch',  shortTechnicalDescription: 'Monitors Azure spend.'           }
  ])
};

function buildRoute(id: string | null) {
  return {
    snapshot: { paramMap: { get: (_: string) => id } }
  };
}

describe('OpportunityDetail', () => {
  let component: OpportunityDetail;
  let fixture: ComponentFixture<OpportunityDetail>;
  let oppService: jasmine.SpyObj<OpportunityService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    oppService = jasmine.createSpyObj('OpportunityService',
      ['getOpportunity', 'synthesizeIdeas', 'createProductFromOpportunity']);
    router = jasmine.createSpyObj('Router', ['navigate']);

    oppService.getOpportunity.and.returnValue(of(PENDING_OPP));

    await TestBed.configureTestingModule({
      imports: [OpportunityDetail],
      providers: [
        { provide: OpportunityService, useValue: oppService },
        { provide: Router,             useValue: router     },
        { provide: ActivatedRoute,     useValue: buildRoute('1') }
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(OpportunityDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Initialization ───────────────────────────────────────────────────────

  it('loads opportunity on init', () => {
    expect(oppService.getOpportunity).toHaveBeenCalledWith(1);
    expect(component.opportunity).toEqual(PENDING_OPP);
  });

  it('redirects to /opportunities when id is missing', async () => {
    await TestBed.resetTestingModule();

    router = jasmine.createSpyObj('Router', ['navigate']);
    await TestBed.configureTestingModule({
      imports: [OpportunityDetail],
      providers: [
        { provide: OpportunityService, useValue: oppService },
        { provide: Router,             useValue: router     },
        { provide: ActivatedRoute,     useValue: buildRoute(null) }
      ]
    }).compileComponents();

    const f = TestBed.createComponent(OpportunityDetail);
    f.detectChanges();
    expect(router.navigate).toHaveBeenCalledWith(['/opportunities']);
  });

  // ── productIdeas getter ──────────────────────────────────────────────────

  it('productIdeas returns empty array when no json', () => {
    component.opportunity = PENDING_OPP;
    expect(component.productIdeas).toEqual([]);
  });

  it('productIdeas parses json from completed opportunity', () => {
    component.opportunity = COMPLETED_OPP;
    const ideas = component.productIdeas;
    expect(ideas.length).toBe(3);
    expect(ideas[0].name).toBe('Audit Bot');
    expect(ideas[1].name).toBe('CI/CD Suite');
    expect(ideas[2].name).toBe('Cost Watch');
  });

  it('productIdeas returns empty array on malformed json', () => {
    component.opportunity = { ...PENDING_OPP, productIdeasJson: '{{bad json' };
    expect(component.productIdeas).toEqual([]);
  });

  // ── statusBadgeClass getter ───────────────────────────────────────────────

  it('statusBadgeClass returns green for completed', () => {
    component.opportunity = COMPLETED_OPP;
    expect(component.statusBadgeClass).toContain('green');
  });

  it('statusBadgeClass returns amber for pending', () => {
    component.opportunity = PENDING_OPP;
    expect(component.statusBadgeClass).toContain('amber');
  });

  it('statusBadgeClass returns red for failed', () => {
    component.opportunity = { ...PENDING_OPP, llmStatus: 'failed' };
    expect(component.statusBadgeClass).toContain('red');
  });

  // ── analyzeOpportunity ───────────────────────────────────────────────────

  it('analyzeOpportunity updates opportunity on success', fakeAsync(() => {
    oppService.synthesizeIdeas.and.returnValue(of(COMPLETED_OPP));
    component.analyzeOpportunity();
    tick();
    expect(component.opportunity?.llmStatus).toBe('completed');
    expect(component.synthesizing).toBeFalse();
  }));

  it('analyzeOpportunity sets synthesisError on failure', fakeAsync(() => {
    oppService.synthesizeIdeas.and.returnValue(throwError(() => new Error('AI error')));
    component.analyzeOpportunity();
    tick();
    expect(component.synthesisError).toBeTruthy();
    expect(component.synthesizing).toBeFalse();
  }));

  it('analyzeOpportunity is a no-op if already synthesizing', () => {
    component.synthesizing = true;
    component.analyzeOpportunity();
    expect(oppService.synthesizeIdeas).not.toHaveBeenCalled();
  });

  // ── createProduct ────────────────────────────────────────────────────────

  it('createProduct adds index to createdProductIds on success', fakeAsync(() => {
    const mockProduct = { id: 99, productName: 'Audit Bot' } as any;
    oppService.createProductFromOpportunity.and.returnValue(of(mockProduct));

    const idea = { name: 'Audit Bot', shortTechnicalDescription: 'Automates audits.' };
    component.createProduct(idea, 0);
    tick();

    expect(component.createdProductIds.has(0)).toBeTrue();
    expect(component.creatingProductIds.has(0)).toBeFalse();
  }));

  it('createProduct sets createErrors on failure', fakeAsync(() => {
    oppService.createProductFromOpportunity.and.returnValue(throwError(() => new Error('API error')));

    const idea = { name: 'Audit Bot', shortTechnicalDescription: 'Automates audits.' };
    component.createProduct(idea, 0);
    tick();

    expect(component.createErrors.has(0)).toBeTrue();
    expect(component.creatingProductIds.has(0)).toBeFalse();
  }));

  it('createProduct is a no-op if already creating that index', () => {
    component.creatingProductIds.add(0);
    const idea = { name: 'Audit Bot', shortTechnicalDescription: 'Automates audits.' };
    component.createProduct(idea, 0);
    expect(oppService.createProductFromOpportunity).not.toHaveBeenCalled();
  });

  it('createProduct is a no-op if already created that index', () => {
    component.createdProductIds.add(0);
    const idea = { name: 'Audit Bot', shortTechnicalDescription: 'Automates audits.' };
    component.createProduct(idea, 0);
    expect(oppService.createProductFromOpportunity).not.toHaveBeenCalled();
  });
});
