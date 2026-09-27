"""Bounded numerical design study. No production, Unity scene or device mutation.

Only existing sphere-center vectors supply reach; there is no hand-openness
classifier. Optional private replay uses the recorded rangeInput (which already
includes the old geometric limit law), not a newly recomputed sphere fit.
Run with Python + numpy; --plot additionally requires matplotlib.
"""
import argparse
import csv
import json
import math
from pathlib import Path

import numpy as np


# Illustrative sphere-distance anchors, not measurements of subjective % opening.
NEAR_SPHERE = .08
NEAR_REACH = 3.0
FAR_SPHERE = .12
FAR_REACH = 100.0
def near_range(s):
    # Retain the legacy low-order part: unit slope at zero. A previous pure
    # linear 28.6x preset enlarged the tiny near-palm motions and was rejected.
    return s + s*s/.02


TAIL = FAR_REACH - near_range(FAR_SPHERE)
minimum_power = math.log((NEAR_REACH-near_range(NEAR_SPHERE))/TAIL) / math.log(NEAR_SPHERE / FAR_SPHERE)
# Smallest odd power satisfying the working-volume bound. The added radial
# term is polynomial in q and q.q; the retained quadratic term is C1 at zero.
POWER = math.ceil(minimum_power)
POWER += (POWER % 2 == 0)


def legacy_range(s):
    return s + s*s/.02 + .02*(s/.03)**6


def proposed_range(s):
    return near_range(s) + TAIL*(s/FAR_SPHERE)**POWER


def proposed_slope(s):
    return 1 + 2*s/.02 + TAIL*POWER/FAR_SPHERE*(s/FAR_SPHERE)**(POWER-1)


def legacy_slope(s):
    return 1 + 2*s/.02 + 4*(s/.03)**5


def unit(v):
    length = np.linalg.norm(v)
    return v / length if length > 0 else np.zeros(3)


def angle(a, b):
    return math.atan2(np.linalg.norm(np.cross(a, b)), float(np.dot(a, b)))


def alpha(cutoff, dt):
    return 1 / (1 + 1/(2*math.pi*cutoff*dt))


def rotate_y(degrees):
    a = math.radians(degrees)
    return np.array([[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]])


def great_circle(a, b, amount):
    arc = angle(a, b)
    tangent = b - a*np.dot(a, b)
    length = np.linalg.norm(tangent)
    if length < 1e-12:
        if np.dot(a, b) >= 0:
            return b.copy()
        # At exact antipodes a shortest path is nonunique. Deterministic study
        # convention only; no globally continuous antipodal tangent exists.
        basis = np.eye(3)[np.argmin(np.abs(a))]
        tangent = unit(np.cross(a, basis))
    else:
        tangent /= length
    return unit(a*math.cos(arc*amount) + tangent*math.sin(arc*amount))


class SphereFilter:
    """Exploratory radial + geodesic angular filtering in the current palm frame.

    Speed-adaptive first-order filtering follows the 1 Euro principle. These are
    independently written equations, not copied implementation code. Fixed
    parameters illustrate behavior; they are not perceptually tuned defaults.
    """
    def __init__(self):
        self.s = None
        self.n = None
        self.previous = None
        self.ds = 0.
        self.omega = np.zeros(3)

    def step(self, q, dt):
        assert np.isfinite(q).all() and math.isfinite(dt) and 0 < dt <= .25
        s = np.linalg.norm(q)
        n = unit(q)
        if self.s is None or s == 0 or self.s == 0:
            # Preserve the already-required exact fist endpoint. Origin has no
            # direction; the next nonzero sample establishes it. This is an
            # explicit endpoint contract, not an openness/range classifier.
            self.s, self.n, self.previous = s, n, q.copy()
            self.ds = 0.; self.omega[:] = 0
            return q.copy()
        old_s = np.linalg.norm(self.previous)
        old_n = unit(self.previous)
        derivative_alpha = alpha(5, dt)
        self.ds += derivative_alpha*((s-old_s)/dt-self.ds)
        cross = np.cross(old_n, n)
        w = unit(cross)*angle(old_n, n)/dt
        self.omega += derivative_alpha*(w-self.omega)
        # Separate physical units: metres/s before expansion and radians/s.
        radial_alpha = alpha(2 + 100*abs(self.ds), dt)
        angular_alpha = alpha(8 + 4*np.linalg.norm(self.omega), dt)
        self.s += radial_alpha*(s-self.s)
        self.n = great_circle(self.n, n, angular_alpha)
        self.previous = q.copy()
        return self.n*self.s


def simulate(out):
    out.mkdir(parents=True, exist_ok=True)
    report = {
        'scope': 'Double-precision numerical experiment, not a Unity/Udon implementation or deployment.',
        'input': 'Existing sphere-derived center-minus-hand vector. No new openness feature.',
        'curve': dict(near_sphere_m=NEAR_SPHERE, maximum_near_reach_m=NEAR_REACH,
                      far_sphere_m=FAR_SPHERE, far_reach_m=FAR_REACH,
                      derived_power=POWER, origin_gain=1, tail_coefficient=TAIL),
        'response': [], 'jitter': [], 'limitations': [
            'Sphere-distance anchors are illustrative, not calibrated to 65/80/90 percent hand opening.',
            'Independent radial/angular filtering needs near-origin and antipodal design review.',
            'Current-palm transport adds no filter lag to rigid pose, but also passes palm-pose noise.',
            'No tracking ground truth, perceptual tuning, raw joint API improvement or runtime performance evidence.',
            'The candidate power-law tail grows much faster than legacy at extreme input; runtime finite-range handling is unresolved.',
        ],
    }
    assert POWER % 2 == 1 and TAIL > 0
    assert proposed_range(0) == 0 and proposed_slope(0) == 1
    assert abs(proposed_range(FAR_SPHERE)-FAR_REACH) < 1e-10
    assert proposed_range(NEAR_SPHERE) <= NEAR_REACH
    for s in np.linspace(0,NEAR_SPHERE,1000):
        assert proposed_slope(s) <= legacy_slope(s)+1e-10
    distances = np.linspace(0, 2, 20001)
    assert np.all(np.diff(proposed_range(distances)) > 0)
    with (out/'sphere-range-law.csv').open('w', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(['sphere_center_distance_m','legacy_reach_m','candidate_reach_m','legacy_gain','candidate_gain'])
        for s in [.0,.005,.01,.02,.03,.04,.05,.06,.07,.08,.09,.1,.12,.15,.2,.3,1,2]:
            writer.writerow([s,legacy_range(s),proposed_range(s),legacy_slope(s),proposed_slope(s)])

    for hz in [30,72,120]:
        dt = 1/hz
        for degrees in [1,90,170,180]:
            responses = []
            for s in [.005,.02,.07,.12,.5,2]:
                filt = SphereFilter()
                for _ in range(hz):
                    filt.step(np.array([0.,0.,s]),dt)
                target = rotate_y(degrees)@np.array([0.,0.,s])
                if degrees == 180:
                    target=np.array([0.,0.,-s])
                arrival = None; minimum = 1.
                for i in range(hz):
                    value = filt.step(target,dt)
                    minimum = min(minimum, proposed_range(np.linalg.norm(value))/proposed_range(s))
                    if arrival is None and angle(unit(value),unit(target)) <= math.radians(degrees*.1):
                        arrival = (i+1)/hz
                assert arrival is not None and arrival <= .1
                assert abs(minimum-1) < 1e-10
                responses.append(arrival)
                report['response'].append(dict(hz=hz,angle_deg=degrees,sphere_distance_m=s,
                    candidate_reach_m=proposed_range(s),angular_90pct_seconds=arrival,
                    minimum_radius_fraction=minimum))
            assert max(responses)-min(responses) < dt/2
        # Input center-distance noise, not output noise or percentage opening.
        rng = np.random.default_rng(7392)
        for s in [.02,.07,.12]:
            filt=SphereFilter(); raw_radial=[]; shown_radial=[]; raw_angle=[]; shown_angle=[]
            for i in range(hz*10):
                noisy_s=s+rng.normal(0,.0002)
                noisy_angle=rng.normal(0,.05)
                q=rotate_y(noisy_angle)@np.array([0.,0.,noisy_s])
                v=filt.step(q,dt)
                if i>=hz:
                    raw_radial.append(proposed_range(noisy_s)-proposed_range(s))
                    shown_radial.append(proposed_range(np.linalg.norm(v))-proposed_range(s))
                    raw_angle.append(noisy_angle)
                    shown_angle.append(math.degrees(math.atan2(v[0],v[2])))
            rms=lambda x:float(np.sqrt(np.mean(np.square(x))))
            assert rms(shown_radial)<rms(raw_radial) and rms(shown_angle)<rms(raw_angle)
            report['jitter'].append(dict(hz=hz,sphere_distance_m=s,raw_radial_rms_m=rms(raw_radial),
                filtered_radial_rms_m=rms(shown_radial),raw_angle_rms_deg=rms(raw_angle),
                filtered_angle_rms_deg=rms(shown_angle)))

    # Rigid palm motion transports history instead of asking the reach filter to
    # traverse the very long world-space arc. Reflection changes chirality only.
    for mirrored in [False,True]:
        filt=SphereFilter(); local=np.array([.02,.01,.12]); local[0]*=-1 if mirrored else 1
        for i in range(180):
            frame=rotate_y(i*2); root=np.array([math.sin(i*.04),1,0])
            state=filt.step(local,1/72)
            p=root+frame@unit(state)*proposed_range(np.linalg.norm(state))
            expected=root+frame@unit(local)*proposed_range(np.linalg.norm(local))
            assert np.linalg.norm(p-expected)<1e-8
    filt=SphereFilter();filt.step(np.array([0.,0.,2.]),1/72)
    assert np.array_equal(filt.step(np.zeros(3),1/72),np.zeros(3))
    report['contracts']='PASS: curve anchors, monotonicity, direction/range decoupling, 30/72/120Hz, rigid/mirrored transport, zero endpoint.'
    (out/'sphere-space-study.json').write_text(json.dumps(report,indent=2)+'\n')
    return report


def replay(path, out):
    filters={}; last={}; counts={'Left':0,'Right':0}; max_basis_error=0.
    for line in path.open():
        row=json.loads(line); hand=row['hand']; t=row['time']
        if hand in last: assert t>last[hand]
        dt=t-last.get(hand,t-1/72); last[hand]=t
        if not row['tracked'] or dt>.25:
            filters.pop(hand,None);continue
        vec=lambda x:np.array([x[k] for k in ['x','y','z']],dtype=float)
        joints=np.array([vec(j) for j in row['joints']]); normal=unit(vec(row['palmNormal']))
        forward=np.mean(joints[[4,8,12,16]],axis=0)-joints[0]
        forward=unit(forward-normal*np.dot(forward,normal))
        basis=np.column_stack([np.cross(forward,normal),forward,normal])
        max_basis_error=max(max_basis_error,float(np.max(np.abs(basis.T@basis-np.eye(3)))))
        q=basis.T@vec(row['rangeInput'])
        filt=filters.setdefault(hand,SphereFilter()); state=filt.step(q,dt)
        result=vec(row['root'])+basis@unit(state)*proposed_range(np.linalg.norm(state))
        assert np.isfinite(result).all()
        counts[hand]+=1
    summary=dict(tracked_samples=counts,max_basis_orthogonality_error=max_basis_error,
        conclusion='Finite replay only. Existing OpenXR limit-corrected vector, not VRChat hand fidelity or a static-noise/intent benchmark. No per-frame output retained.')
    (out/'private-replay-summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    return summary


def plot(out):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig, axes=plt.subplots(1,3,figsize=(15,4.7),layout='constrained')
    fig.suptitle('Sphere-derived control: a numerical design study, not a deployed change',fontsize=15)
    s=np.linspace(0,.09,500)
    axes[0].plot(s*100,legacy_range(s),label='Current range law',color='#b96038')
    axes[0].plot(s*100,proposed_range(s),label='Illustrative smooth curve',color='#126b85')
    axes[0].plot(s*100,near_range(s),':',label='Retained near-palm terms',color='#126b85')
    axes[0].axvline(NEAR_SPHERE*100,color='.65',lw=.8);axes[0].set_ylim(0,5)
    axes[0].set(xlabel='Sphere-center distance from hand (cm)',ylabel='Cursor range (m)',title='Nearby: allocate more sphere motion to precision')
    s=np.linspace(.001,.3,800)
    axes[1].semilogy(s*100,legacy_range(s),color='#b96038');axes[1].semilogy(s*100,proposed_range(s),color='#126b85')
    axes[1].scatter([8,12],[proposed_range(.08),100],color='#126b85',s=22)
    axes[1].set(xlabel='Sphere-center distance from hand (cm)',ylabel='Cursor range (m, log scale)',title='One curve; no pose bands or range cap')
    # Same constant filter gain isolates the interpolation geometry alone.
    n=np.array([0.,0.,1.]);q=n.copy();ranges=[];product=[];times=[]
    for i in range(25):
        a=alpha(8,1/72);q=(1-a)*q+a*np.array([1.,0.,0.]);n=great_circle(n,np.array([1.,0.,0.]),a)
        ranges.append(proposed_range(np.linalg.norm(q)*.12)/100)
        product.append(proposed_range(np.linalg.norm(n)*.12)/100);times.append((i+1)/72)
    axes[2].plot(times,ranges,label='Average unexpanded 3D vectors',color='#b96038')
    axes[2].plot(times,product,label='Filter direction and length separately',color='#126b85')
    axes[2].set(xlabel='Time after 90° direction step (s)',ylabel='Fraction of intended range',ylim=(0,1.08),title='Avoid contraction during a pure turn')
    for ax in axes:ax.grid(alpha=.2)
    axes[0].legend(fontsize=8,loc='upper left');axes[2].legend(fontsize=8,loc='lower right')
    fig.savefig(out/'sphere-space-study.png',dpi=150)
    plt.close(fig)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--plot',action='store_true')
    parser.add_argument('--joint-trace',type=Path)
    args=parser.parse_args();report=simulate(args.out)
    print(report['contracts']);print('Derived smooth radial power:',POWER)
    if args.joint_trace:print(json.dumps(replay(args.joint_trace,args.out)))
    if args.plot:plot(args.out)
